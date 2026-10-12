#!/usr/bin/env python3
"""
AcxiomCRM end-to-end acceptance tests (HTTP level).

Runs the final acceptance scenarios from section 17.19 of the specification plus the security,
validation, workflow, audit, API and report checks against a running instance seeded with the
development demo data.

Usage:
    pip install requests
    python tests/acceptance_test.py --base-url http://localhost:5173 --password "<Seed:DemoPassword>"

The script creates its own test records (customers, leads, users) with unique names, so it can be
run repeatedly. It does not modify the five demo accounts except to read their data.
"""
import argparse
import csv
import io
import json
import re
import sys
import time
import uuid
from datetime import date, datetime, timedelta

import requests

ADMIN = "admin@acxiomcrm.local"
MANAGER = "manager@acxiomcrm.local"
SALES1 = "rohan.das@acxiomcrm.local"       # reports to the manager
SALES2 = "sneha.patnaik@acxiomcrm.local"   # reports to the manager
SALES3 = "vikram.rao@acxiomcrm.local"      # no manager (outside the manager's team)

TOKEN_RE = re.compile(r'name="__RequestVerificationToken" type="hidden" value="([^"]+)"')

results = []


def check(name, condition, detail=""):
    results.append((name, bool(condition), detail))
    mark = "PASS" if condition else "FAIL"
    print(f"[{mark}] {name}" + (f"  -- {detail}" if detail and not condition else ""))
    return condition


class Client:
    def __init__(self, base):
        self.base = base.rstrip("/")
        self.s = requests.Session()

    def url(self, path):
        return self.base + path

    def get(self, path, **kw):
        kw.setdefault("allow_redirects", False)
        return self.s.get(self.url(path), **kw)

    def token(self, path):
        r = self.s.get(self.url(path))
        m = TOKEN_RE.search(r.text)
        if not m:
            raise RuntimeError(f"No anti-forgery token on {path} (status {r.status_code})")
        return m.group(1)

    def post_form(self, form_path, post_path, data, token_path=None):
        data = dict(data)
        data["__RequestVerificationToken"] = self.token(token_path or form_path)
        return self.s.post(self.url(post_path), data=data, allow_redirects=False)

    def login(self, email, password):
        r = self.post_form("/Account/Login", "/Account/Login", {"Email": email, "Password": password})
        return r

    def api(self, method, path, **kw):
        return self.s.request(method, self.url(path), allow_redirects=False, **kw)


def location_id(resp):
    loc = resp.headers.get("Location", "")
    m = re.search(r"/Details/(\d+)", loc)
    return int(m.group(1)) if m else None


def unique():
    return uuid.uuid4().hex[:6]


def unique_phone():
    # 10-digit Indian mobile number starting with 9, unique per run
    return "9" + str(int(time.time() * 1000) % 1_000_000_000).zfill(9)


def audit_rows(admin, **params):
    params = {k: v for k, v in params.items() if v is not None}
    params["export"] = "csv"
    r = admin.get("/Reports/Audit", params=params)
    text = r.content.decode("utf-8-sig")
    return list(csv.DictReader(io.StringIO(text)))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base-url", default="http://localhost:5173")
    ap.add_argument("--password", required=True, help="Demo account password (Seed:DemoPassword)")
    args = ap.parse_args()
    base, pw = args.base_url, args.password
    today = date.today()
    yesterday = (today - timedelta(days=1)).isoformat()
    future = (today + timedelta(days=20)).isoformat()

    # ------------------------------------------------------------------ 1. anonymous access
    anon = Client(base)
    for path in ["/", "/Customers", "/Leads", "/Opportunities", "/FollowUps", "/Reports", "/Users", "/AuditLogs"]:
        r = anon.get(path)
        check(f"1. Anonymous {path} is redirected to login", r.status_code == 302 and "/Account/Login" in r.headers.get("Location", ""),
              f"status {r.status_code}")
    r = anon.api("GET", "/api/customers")
    check("1. Anonymous GET /api/customers returns 401", r.status_code == 401, f"status {r.status_code}")

    # ------------------------------------------------------------------ 2. register + login -> dashboard
    reg = Client(base)
    reg_email = f"test.user.{unique()}@example.com"
    weak = reg.post_form("/Account/Register", "/Account/Register",
                         {"FullName": "Policy Test", "Email": reg_email, "Password": "password", "ConfirmPassword": "password"})
    check("Password policy rejects a weak password on registration", weak.status_code == 200 and "field-validation-error" in weak.text)
    strong_pw = "Valid#Pass9"
    r = reg.post_form("/Account/Register", "/Account/Register",
                      {"FullName": "Test Registrant", "Email": reg_email, "Password": strong_pw, "ConfirmPassword": strong_pw})
    check("2. Register redirects to the Dashboard", r.status_code == 302 and r.headers.get("Location", "") in ("/", "/Dashboard"),
          f"{r.status_code} {r.headers.get('Location')}")
    dash = reg.get("/Dashboard")
    check("2. Registered user reaches the Dashboard", dash.status_code == 200 and "Total Customers" in dash.text)

    sales = Client(base)
    r = sales.login(SALES1, pw)
    check("2. Valid login redirects to the Dashboard", r.status_code == 302 and r.headers.get("Location", "") in ("/", "/Dashboard"),
          f"{r.status_code} {r.headers.get('Location')}")
    bad = Client(base).login(SALES1, pw + "x")
    check("Invalid credentials are rejected", bad.status_code == 200 and "Invalid email or password" in bad.text)

    # ------------------------------------------------------------------ 3/4. client + server validation on customers
    form = sales.get("/Customers/Create").text
    check("3. Customer form renders client-side email rule", 'data-val-regex="Enter a valid email address."' in form)
    check("3. Customer form renders client-side phone rule", 'data-val-regex="Enter a valid phone number."' in form)
    check("3. Customer form renders required rules", 'data-val-required="Customer Name is required."' in form)
    check("3. Customer form renders length rules", 'data-val-length-max="100"' in form)

    before = sales.api("GET", "/api/customers", params={"pageSize": 1}).json()["totalCount"]
    r = sales.post_form("/Customers/Create", "/Customers/Create",
                        {"CustomerName": "Bypass Test", "Email": "not-an-email", "Phone": "12345", "Status": "Active"})
    check("4. Crafted request with invalid email/phone is rejected by the server",
          r.status_code == 200 and "Enter a valid email address." in r.text and "Enter a valid phone number." in r.text)
    r = sales.post_form("/Customers/Create", "/Customers/Create", {"CustomerName": "", "Email": "", "Phone": ""})
    check("4. Server rejects missing required fields with field messages",
          "Customer Name is required." in r.text and "Email is required." in r.text and "Phone is required." in r.text)
    r = sales.post_form("/Customers/Create", "/Customers/Create",
                        {"CustomerName": "X" * 101, "Email": f"long{unique()}@example.com", "Phone": unique_phone()})
    check("4. Server rejects text longer than the maximum length", "cannot exceed 100 characters" in r.text)
    after = sales.api("GET", "/api/customers", params={"pageSize": 1}).json()["totalCount"]
    check("4. No customer was saved by the invalid requests", before == after)

    r = sales.s.post(sales.url("/Customers/Create"), data={"CustomerName": "No token", "Email": f"nt{unique()}@example.com",
                                                           "Phone": unique_phone()}, allow_redirects=False)
    check("Anti-forgery: POST without token is rejected", r.status_code == 400, f"status {r.status_code}")

    # valid create + duplicate prevention
    cust_email = f"acceptance.{unique()}@example.com"
    cust_phone = unique_phone()
    r = sales.post_form("/Customers/Create", "/Customers/Create",
                        {"CustomerName": "Acceptance Customer", "Email": cust_email, "Phone": cust_phone, "CompanyName": "Test Co",
                         "City": "Bhubaneswar", "State": "Odisha", "Status": "Active"})
    customer_id = location_id(r)
    check("Valid customer is created", r.status_code == 302 and customer_id is not None, f"{r.status_code} {r.headers.get('Location')}")
    r = sales.post_form("/Customers/Create", "/Customers/Create",
                        {"CustomerName": "Duplicate", "Email": cust_email.upper(), "Phone": unique_phone(), "Status": "Active"})
    check("Duplicate customer email is rejected (case-insensitive)", "A customer with this email already exists." in r.text)
    r = sales.post_form("/Customers/Create", "/Customers/Create",
                        {"CustomerName": "Duplicate", "Email": f"other{unique()}@example.com", "Phone": cust_phone, "Status": "Active"})
    check("Duplicate customer phone is rejected", "A customer with this phone number already exists." in r.text)
    r = sales.get("/Customers/CheckEmail", params={"email": cust_email})
    check("Remote duplicate check reports existing email", r.json() != True)

    # update -> audit
    r = sales.post_form(f"/Customers/Edit/{customer_id}", f"/Customers/Edit/{customer_id}",
                        {"CustomerId": customer_id, "CustomerName": "Acceptance Customer Updated", "Email": cust_email,
                         "Phone": cust_phone, "CompanyName": "Test Co", "City": "Cuttack", "State": "Odisha", "Status": "Active"})
    check("Customer can be edited", r.status_code == 302)

    # ------------------------------------------------------------------ 5/6/7. opportunity rules
    opp_base = {"OpportunityName": "Acceptance Deal", "CustomerId": customer_id, "Stage": "Qualification",
                "Probability": 30, "Amount": 150000, "ExpectedCloseDate": future}
    r = sales.post_form("/Opportunities/Create", "/Opportunities/Create", {**opp_base, "Amount": 0})
    check("5. Opportunity with Amount = 0 is rejected", "Opportunity Amount must be greater than 0." in r.text)
    r = sales.post_form("/Opportunities/Create", "/Opportunities/Create", {**opp_base, "Amount": -10})
    check("5. Opportunity with negative Amount is rejected", "Opportunity Amount must be greater than 0." in r.text)
    r = sales.post_form("/Opportunities/Create", "/Opportunities/Create", {**opp_base, "Probability": 101})
    check("6. Opportunity with Probability = 101 is rejected", "Probability must be between 0 and 100." in r.text)
    r = sales.post_form("/Opportunities/Create", "/Opportunities/Create", {**opp_base, "ExpectedCloseDate": yesterday})
    check("7. Active opportunity with past Expected Close Date is rejected", "Expected Close Date cannot be in the past." in r.text)

    r = sales.api("POST", "/api/opportunities", json={**opp_base, "Amount": 0})
    check("5. API rejects Amount = 0 with 400", r.status_code == 400 and "Amount" in r.text, f"{r.status_code} {r.text[:200]}")
    r = sales.api("POST", "/api/opportunities", json={**opp_base, "Probability": 150})
    check("6. API rejects Probability > 100 with 400", r.status_code == 400, f"{r.status_code}")
    r = sales.api("POST", "/api/opportunities", json={**opp_base, "ExpectedCloseDate": yesterday})
    check("7. API rejects past Expected Close Date with 400", r.status_code == 400, f"{r.status_code}")
    r = sales.api("POST", "/api/opportunities", json={**opp_base, "Stage": "Won"})
    check("New opportunity cannot start as Won", r.status_code == 400, f"{r.status_code}")

    r = sales.post_form("/Opportunities/Create", "/Opportunities/Create", opp_base)
    opp_id = location_id(r)
    check("Valid opportunity is created", opp_id is not None, f"{r.status_code}")
    detail = sales.get(f"/Opportunities/Details/{opp_id}").text
    check("Weighted amount = Amount x Probability / 100 is shown", "₹45,000" in detail)
    r = sales.post_form(f"/Opportunities/Details/{opp_id}", f"/Opportunities/Close/{opp_id}", {"outcome": "Won", "remarks": "Signed"})
    detail = sales.get(f"/Opportunities/Details/{opp_id}").text
    check("Opportunity can be marked Won (final outcome captured)", "Won" in detail and "100%" in detail)
    r = sales.post_form(f"/Opportunities/Details/{opp_id}", f"/Opportunities/Close/{opp_id}", {"outcome": "Lost"})
    detail = sales.get(f"/Opportunities/Details/{opp_id}", allow_redirects=True).text
    check("Closed opportunity cannot be re-closed", "already Won" in detail)

    # ------------------------------------------------------------------ 8. follow-up date rule + workflow
    fu_base = {"RelatedType": "Customer", "CustomerId": customer_id, "Subject": "Acceptance follow-up",
               "FollowUpType": "Call", "FollowUpDate": f"{(today + timedelta(days=2)).isoformat()}T10:30"}
    r = sales.post_form("/FollowUps/Create", "/FollowUps/Create", {**fu_base, "FollowUpDate": f"{yesterday}T10:00"})
    check("8. Follow-up dated before today is rejected", "Follow-up date cannot be earlier than today." in r.text)
    r = sales.api("POST", "/api/followups", json={"Subject": "API past", "FollowUpType": "Call", "FollowUpDate": f"{yesterday}T10:00:00",
                                                  "CustomerId": customer_id})
    check("8. API rejects past follow-up date with 400", r.status_code == 400, f"{r.status_code}")
    r = sales.post_form("/FollowUps/Create", "/FollowUps/Create", fu_base)
    fu_id = location_id(r)
    check("Follow-up is scheduled", fu_id is not None, f"{r.status_code}")
    r = sales.post_form(f"/FollowUps/Details/{fu_id}", f"/FollowUps/Reschedule/{fu_id}",
                        {"Reschedule.NewDate": f"{yesterday}T09:00", "Reschedule.Reason": "test"})
    page = sales.get(f"/FollowUps/Details/{fu_id}").text
    check("8. Reschedule to a past date is rejected", "Follow-up date cannot be earlier than today." in page)
    r = sales.post_form(f"/FollowUps/Details/{fu_id}", f"/FollowUps/Reschedule/{fu_id}",
                        {"Reschedule.NewDate": f"{(today + timedelta(days=4)).isoformat()}T09:00", "Reschedule.Reason": "Client asked"})
    page = sales.get(f"/FollowUps/Details/{fu_id}").text
    check("Follow-up can be rescheduled", "rescheduled" in page.lower())
    r = sales.post_form(f"/FollowUps/Details/{fu_id}", f"/FollowUps/Complete/{fu_id}", {"Complete.Outcome": "Discussed renewal"})
    page = sales.get(f"/FollowUps/Details/{fu_id}").text
    check("Follow-up can be completed", "Completed" in page)
    acts = sales.get("/Activities", params={"q": "Acceptance follow-up"}).text
    check("Completing a follow-up logs an activity on the related record", "Acceptance follow-up" in acts)

    # ------------------------------------------------------------------ lead workflow + conversion
    r = sales.post_form("/Leads/Create", "/Leads/Create",
                        {"LeadName": f"Acceptance Lead {unique()}", "Email": "bad-email", "Source": "Website", "Status": "New",
                         "Priority": "High", "ExpectedValue": 50000})
    check("Lead email must follow the email rule", "Enter a valid email address." in r.text)
    r = sales.post_form("/Leads/Create", "/Leads/Create",
                        {"LeadName": "Status Tamper", "Source": "Website", "Status": "99", "Priority": "High", "ExpectedValue": 1})
    check("Lead status outside the valid set is rejected (tampered value)",
          r.status_code == 200 and ("Select a valid" in r.text or "is not valid" in r.text))
    r = sales.api("POST", "/api/leads", json={"LeadName": "API bad status", "Source": "Website", "Status": "Bogus", "Priority": "Low", "ExpectedValue": 1})
    check("API rejects an invalid lead status with 400", r.status_code == 400, f"{r.status_code}")
    r = sales.post_form("/Leads/Create", "/Leads/Create",
                        {"LeadName": "Converted Directly", "Source": "Website", "Status": "Converted", "Priority": "High", "ExpectedValue": 1})
    check("A new lead cannot be created as Converted", r.status_code == 200 and "Convert" in r.text)

    lead_email = f"lead.{unique()}@example.com"
    lead_phone = unique_phone()
    r = sales.post_form("/Leads/Create", "/Leads/Create",
                        {"LeadName": "Acceptance Lead", "Email": lead_email, "Phone": lead_phone, "CompanyName": "Lead Co",
                         "Source": "Referral", "Status": "New", "Priority": "High", "ExpectedValue": 250000})
    lead_id = location_id(r)
    check("Lead is created", lead_id is not None)
    lead_fields = {"LeadName": "Acceptance Lead", "Email": lead_email, "Phone": lead_phone, "CompanyName": "Lead Co",
                   "Source": "Referral", "Priority": "High", "ExpectedValue": 250000}
    r = sales.post_form(f"/Leads/Edit/{lead_id}", f"/Leads/Edit/{lead_id}", {**lead_fields, "Status": "Qualified"})
    check("Invalid lead status transition (New -> Qualified) is rejected", r.status_code == 200 and "cannot change from New to Qualified" in r.text)
    r = sales.get(f"/Leads/Convert/{lead_id}")
    check("Conversion is blocked until the lead is Qualified", r.status_code == 302)
    for st in ["Contacted", "Qualified"]:
        r = sales.post_form(f"/Leads/Edit/{lead_id}", f"/Leads/Edit/{lead_id}", {**lead_fields, "Status": st})
        check(f"Lead moves to {st}", r.status_code == 302)
    r = sales.post_form(f"/Leads/Convert/{lead_id}", f"/Leads/Convert/{lead_id}",
                        {"CreateNewCustomer": "true", "CustomerName": "Acceptance Lead", "Email": lead_email, "Phone": lead_phone,
                         "CompanyName": "Lead Co", "CreateOpportunity": "true", "OpportunityName": "Lead Co deal", "Amount": 250000,
                         "Probability": 20, "ExpectedCloseDate": future, "Stage": "Qualification"})
    conv_opp = location_id(r)
    check("Qualified lead converts to customer + opportunity", r.status_code == 302 and "/Opportunities/Details/" in r.headers.get("Location", ""),
          f"{r.status_code} {r.headers.get('Location')}")
    lead_page = sales.get(f"/Leads/Details/{lead_id}").text
    check("Lead is marked Converted and linked to the customer", "Converted" in lead_page and "CUST-" in lead_page)
    r = sales.post_form(f"/Leads/Edit/{lead_id}", f"/Leads/Edit/{lead_id}", {**lead_fields, "Status": "New"}, token_path=f"/Leads/Details/{lead_id}")
    check("Converted lead cannot be edited", r.status_code in (200, 302) and "Converted leads cannot be edited." in sales.get(f"/Leads/Edit/{lead_id}", allow_redirects=True).text + r.text
          or r.status_code == 200 and "cannot be edited" in r.text)

    # ------------------------------------------------------------------ 9. sales executive scope
    admin = Client(base)
    check("Admin login", admin.login(ADMIN, pw).status_code == 302)
    all_customers = admin.api("GET", "/api/customers", params={"pageSize": 100}).json()["items"]
    vikram_customer = next(c for c in all_customers if c["assignedTo"]["name"] == "Vikram Rao")
    sneha_customer = next(c for c in all_customers if c["assignedTo"]["name"] == "Sneha Patnaik")

    mine = sales.api("GET", "/api/customers", params={"pageSize": 100}).json()["items"]
    check("9. Sales Executive only sees own/assigned customers",
          all(c["assignedTo"]["name"] == "Rohan Das" or c.get("createdBy", {}).get("name") == "Rohan Das" for c in mine) and len(mine) < len(all_customers))
    r = sales.get(f"/Customers/Details/{vikram_customer['customerId']}")
    check("9. Sales Executive cannot open another user's customer by URL", r.status_code == 302 and "AccessDenied" in r.headers.get("Location", ""),
          f"{r.status_code} {r.headers.get('Location')}")
    r = sales.api("GET", f"/api/customers/{vikram_customer['customerId']}")
    check("9. API returns 403 for another user's customer", r.status_code == 403, f"{r.status_code}")
    r = sales.api("PUT", f"/api/customers/{vikram_customer['customerId']}",
                  json={"CustomerName": "Hijack", "Email": vikram_customer["email"], "Phone": vikram_customer["phone"], "Status": "Active"})
    check("9. API rejects update of another user's customer (403)", r.status_code == 403, f"{r.status_code}")
    r = sales.post_form(f"/Customers/Edit/{customer_id}", f"/Customers/Edit/{vikram_customer['customerId']}",
                        {"CustomerName": "Hijack", "Email": vikram_customer["email"], "Phone": vikram_customer["phone"], "Status": "Active"})
    check("9. Tampered MVC edit of another user's record is rejected", r.status_code in (302, 403) and "AccessDenied" in r.headers.get("Location", "AccessDenied"))
    r = sales.api("DELETE", f"/api/customers/{customer_id}")
    check("9. Sales Executive cannot delete customers (403)", r.status_code == 403, f"{r.status_code}")
    for path in ["/Users", "/Roles", "/AuditLogs", "/Reports/UserActivity", "/Reports/Audit", f"/Customers/Delete/{customer_id}"]:
        r = sales.get(path)
        check(f"9. Sales Executive denied {path}", r.status_code == 302 and "AccessDenied" in r.headers.get("Location", ""), f"{r.status_code}")
    r = sales.post_form("/Customers/Create", "/Customers/Create",
                        {"CustomerName": "Assign Tamper", "Email": f"at{unique()}@example.com", "Phone": unique_phone(), "Status": "Active",
                         "AssignedTo": vikram_customer["assignedTo"]["id"]})
    check("9. Sales Executive cannot assign records to another user", "You cannot assign records to the selected user." in r.text)
    pipe = sales.api("GET", "/api/reports/pipeline").json()
    check("9. Sales Executive pipeline report is scoped to own records", pipe["scope"] == "Own" and all(o["owner"] == "Rohan Das" for o in pipe["byOwner"]))

    # ------------------------------------------------------------------ 10. manager scope
    mgr = Client(base)
    check("Manager login", mgr.login(MANAGER, pw).status_code == 302)
    team = mgr.api("GET", "/api/customers", params={"pageSize": 100}).json()["items"]
    owners = {c["assignedTo"]["name"] for c in team}
    check("10. Manager sees team customers (incl. team members')", {"Rohan Das", "Sneha Patnaik"} <= owners, str(owners))
    check("10. Manager does not see customers outside the team", "Vikram Rao" not in owners, str(owners))
    r = mgr.api("GET", f"/api/customers/{sneha_customer['customerId']}")
    check("10. Manager can open a team member's customer", r.status_code == 200)
    r = mgr.api("GET", f"/api/customers/{vikram_customer['customerId']}")
    check("10. Manager cannot open a customer outside the team (403)", r.status_code == 403)
    r = mgr.get("/Reports/Pipeline")
    check("10. Manager can open the team pipeline report", r.status_code == 200 and "By owner" in r.text)
    pipe = mgr.api("GET", "/api/reports/pipeline").json()
    check("10. Manager pipeline API is team scoped", pipe["scope"] == "Team" and "Vikram Rao" not in {o["owner"] for o in pipe["byOwner"]})
    check("10. Manager can open the user activity report", mgr.get("/Reports/UserActivity").status_code == 200)
    for path in ["/Roles", "/AuditLogs", "/Users/Create", "/Reports/Audit"]:
        r = mgr.get(path)
        check(f"10. Manager has no security administration ({path})", r.status_code == 302 and "AccessDenied" in r.headers.get("Location", ""))
    check("10. Manager can view the team (read-only users list)", mgr.get("/Users").status_code == 200)

    # ------------------------------------------------------------------ 11. admin
    for path in ["/Users", "/Users/Create", "/Roles", "/AuditLogs", "/Reports/Audit", "/Reports/UserActivity"]:
        check(f"11. Admin can open {path}", admin.get(path).status_code == 200)
    admin_all = admin.api("GET", "/api/customers", params={"pageSize": 1}).json()["totalCount"]
    check("11. Admin sees all customers", admin_all >= len(all_customers))

    # user administration: create, role change, deactivate (immediate effect), password policy
    new_email = f"exec.{unique()}@example.com"
    r = admin.post_form("/Users/Create", "/Users/Create", {"FullName": "Policy Check", "Email": new_email, "Role": "SalesExecutive",
                                                            "Password": "short", "ConfirmPassword": "short", "IsActive": "true"})
    check("Admin user creation enforces password policy", r.status_code == 200 and "field-validation-error" in r.text)
    r = admin.post_form("/Users/Create", "/Users/Create", {"FullName": "Temp Executive", "Email": new_email, "Role": "SalesExecutive",
                                                            "Password": "Temp#Pass123", "ConfirmPassword": "Temp#Pass123", "IsActive": "true"})
    loc = r.headers.get("Location", "")
    new_user_id = loc.rsplit("/", 1)[-1] if "/Users/Details/" in loc else None
    check("Admin creates a user", new_user_id is not None, f"{r.status_code} {loc}")
    temp = Client(base)
    check("New user can sign in", temp.login(new_email, "Temp#Pass123").status_code == 302)
    r = admin.post_form(f"/Users/Edit/{new_user_id}", f"/Users/Edit/{new_user_id}",
                        {"Id": new_user_id, "FullName": "Temp Executive", "Email": new_email, "Role": "Manager", "IsActive": "true"})
    check("Admin changes the user's role", r.status_code == 302)
    r = temp.get("/Dashboard")
    check("Role change ends the user's existing session immediately", r.status_code == 302 and "Login" in r.headers.get("Location", ""))
    temp = Client(base)
    temp.login(new_email, "Temp#Pass123")
    check("User signs in again with the new role", temp.get("/Users").status_code == 200)
    r = admin.post_form(f"/Users/Details/{new_user_id}", f"/Users/SetActive/{new_user_id}?active=false", {})
    check("Admin deactivates the user", r.status_code == 302)
    r = temp.get("/Dashboard")
    check("Deactivated user's session is rejected immediately", r.status_code == 302 and "Login" in r.headers.get("Location", ""))
    r = Client(base).login(new_email, "Temp#Pass123")
    check("Deactivated user cannot sign in", r.status_code == 200 and "inactive" in r.text)

    # ------------------------------------------------------------------ lockout
    locked = Client(base)
    lock_email = f"lock.{unique()}@example.com"
    locked.post_form("/Account/Register", "/Account/Register",
                     {"FullName": "Lockout Test", "Email": lock_email, "Password": "Lock#Pass123", "ConfirmPassword": "Lock#Pass123"})
    attacker = Client(base)
    last = None
    for _ in range(5):
        last = attacker.login(lock_email, "Wrong#Pass000")
    check("Lockout: 5 failed attempts lock the account", last is not None and "locked" in last.text.lower())
    r = Client(base).login(lock_email, "Lock#Pass123")
    check("Lockout: correct password is refused while locked", r.status_code == 200 and "locked" in r.text.lower())

    # ------------------------------------------------------------------ 12. audit entries
    rows = audit_rows(admin, entity="Customer", q=None)
    cust_rows = [x for x in rows if x["Record Id"] == str(customer_id)]
    actions = {x["Action"] for x in cust_rows}
    check("12. Customer create is audited", "Create" in actions, str(actions))
    check("12. Customer update is audited", "Update" in actions, str(actions))
    r = admin.post_form(f"/Customers/Delete/{customer_id}", f"/Customers/Delete/{customer_id}", {})
    check("Admin delete of a customer with history deactivates it", r.status_code == 302)
    rows = audit_rows(admin, entity="Customer")
    actions = {x["Action"] for x in rows if x["Record Id"] == str(customer_id)}
    check("12. Customer delete/deactivation is audited", "Deactivate" in actions or "Delete" in actions, str(actions))
    tmp_email = f"del.{unique()}@example.com"
    r = admin.post_form("/Customers/Create", "/Customers/Create", {"CustomerName": "To Delete", "Email": tmp_email, "Phone": unique_phone(), "Status": "Active"})
    del_id = location_id(r)
    r = admin.api("DELETE", f"/api/customers/{del_id}")
    check("API DELETE removes a customer without history", r.status_code == 200 and r.json()["result"] == "Deleted", r.text[:200])
    check("API GET of a deleted customer returns 404", admin.api("GET", f"/api/customers/{del_id}").status_code == 404)
    rows = audit_rows(admin, entity="Customer")
    check("12. Hard delete is audited", any(x["Record Id"] == str(del_id) and x["Action"] == "Delete" for x in rows))
    acct = audit_rows(admin, entity="Account")
    acct_actions = {x["Action"] for x in acct}
    check("12. Login success is audited", "Login" in acct_actions)
    check("12. Failed login is audited", "LoginFailed" in acct_actions)
    check("12. Lockout is audited", "Lockout" in acct_actions)
    user_actions = {x["Action"] for x in audit_rows(admin, entity="User")}
    check("12. Role change is audited", "RoleChange" in user_actions, str(user_actions))
    check("12. Deactivation is audited", "Deactivate" in user_actions, str(user_actions))
    check("12. Lead conversion is audited", any(x["Action"] == "Convert" for x in audit_rows(admin, entity="Lead")))
    check("12. Follow-up completion/reschedule is audited",
          {"Complete", "Reschedule"} <= {x["Action"] for x in audit_rows(admin, entity="FollowUp")})
    r = admin.s.post(admin.url("/AuditLogs/Delete/1"), data={"__RequestVerificationToken": admin.token("/AuditLogs")}, allow_redirects=False)
    check("Audit log has no delete endpoint", r.status_code == 404, f"{r.status_code}")
    detail = admin.get(f"/AuditLogs").text
    check("Audit entries never contain password hashes", "PasswordHash" not in detail and "AQAAAA" not in detail)

    # ------------------------------------------------------------------ 13. REST API
    r = sales.api("GET", "/api/customers")
    check("13. GET /api/customers returns JSON for an authorised user",
          r.status_code == 200 and r.headers.get("Content-Type", "").startswith("application/json") and "items" in r.json())
    check("13. API responses do not expose sensitive fields", "passwordHash" not in r.text.lower() and "securitystamp" not in r.text.lower())
    api = Client(base)
    r = api.api("POST", "/api/auth/login", json={"email": SALES1, "password": pw})
    token = r.json().get("accessToken") if r.status_code == 200 else None
    check("API login issues a bearer token", token is not None, f"{r.status_code} {r.text[:120]}")
    hdr = {"Authorization": f"Bearer {token}"}
    r = requests.get(base + "/api/customers", headers=hdr)
    check("Bearer token authorises GET /api/customers", r.status_code == 200)
    r = requests.post(base + "/api/customers", headers=hdr, json={"CustomerName": "API Customer", "Email": f"api{unique()}@example.com",
                                                                 "Phone": unique_phone(), "Status": "Active"})
    check("POST /api/customers returns 201 Created with Location", r.status_code == 201 and "Location" in r.headers, f"{r.status_code} {r.text[:200]}")
    api_cust = r.json()
    r = requests.post(base + "/api/customers", headers=hdr, json={"CustomerName": "API Dup", "Email": api_cust["email"], "Phone": unique_phone()})
    check("Duplicate via API returns 409 Conflict", r.status_code == 409, f"{r.status_code}")
    r = requests.post(base + "/api/customers", headers=hdr, json={"CustomerName": "", "Email": "x", "Phone": "1"})
    check("Invalid API payload returns 400 with field errors", r.status_code == 400 and "errors" in r.json())
    r = requests.put(base + f"/api/customers/{api_cust['customerId']}", headers=hdr,
                     json={"CustomerName": "API Customer Renamed", "Email": api_cust["email"], "Phone": api_cust["phone"], "Status": "Active"})
    check("PUT /api/customers/{id} updates the record", r.status_code == 200 and r.json()["customerName"] == "API Customer Renamed")
    check("GET /api/customers/{id} for a missing id returns 404", requests.get(base + "/api/customers/999999", headers=hdr).status_code == 404)
    check("GET /api/leads works", requests.get(base + "/api/leads", headers=hdr).status_code == 200)
    r = requests.post(base + "/api/leads", headers=hdr, json={"LeadName": "API Lead", "Source": "Website", "Status": "New", "Priority": "Low", "ExpectedValue": 1000})
    check("POST /api/leads returns 201", r.status_code == 201, f"{r.status_code} {r.text[:200]}")
    check("GET /api/opportunities works", requests.get(base + "/api/opportunities", headers=hdr).status_code == 200)
    r = requests.post(base + "/api/opportunities", headers=hdr, json={"OpportunityName": "API Opp", "CustomerId": api_cust["customerId"], "Amount": 5000,
                                                                     "Stage": "Proposal", "Probability": 50, "ExpectedCloseDate": future})
    check("POST /api/opportunities returns 201", r.status_code == 201, f"{r.status_code} {r.text[:200]}")
    check("GET /api/followups works", requests.get(base + "/api/followups", headers=hdr).status_code == 200)
    r = requests.post(base + "/api/auth/logout", headers=hdr)
    check("POST /api/auth/logout succeeds", r.status_code == 200)
    r = requests.get(base + "/api/customers", headers=hdr)
    check("Bearer token is rejected after API logout", r.status_code == 401, f"{r.status_code}")
    r = requests.post(base + "/api/auth/login", json={"email": SALES1, "password": "nope"})
    check("API login with bad password returns 401", r.status_code == 401)
    r = requests.get(base + "/api/customers", headers={"Authorization": "Bearer invalid"})
    check("Invalid bearer token returns 401", r.status_code == 401)

    # ------------------------------------------------------------------ 14. dashboard
    sales = Client(base)
    sales.login(SALES1, pw)
    page = sales.get("/Dashboard").text
    m = re.search(r'<script type="application/json" id="dashboardData">(.*?)</script>', page, re.S)
    check("14. Dashboard renders KPI cards", all(k in page for k in ["Total Customers", "Total Leads", "Open Leads", "Total Opportunities",
                                                                    "Open Opportunities", "Won Opportunities", "Lost Opportunities", "Total Pipeline Value"]))
    check("14. Dashboard includes Chart.js and chart data", m is not None and "chart.umd.js" in page)
    if m:
        data = json.loads(m.group(1))
        check("14. Lead status chart has the required statuses",
              {"New", "Contacted", "Qualified", "Lost", "Converted"} <= set(data["leadStatus"]["labels"]))
        check("14. Pipeline chart has the required stages",
              data["pipeline"]["labels"] == ["Qualification", "Proposal", "Negotiation", "Won", "Lost"])
        check("14. Monthly sales chart has the configured months", len(data["monthly"]["labels"]) == 12)
        my_leads = sales.api("GET", "/api/leads", params={"pageSize": 100}).json()["totalCount"]
        check("14. Lead chart totals match the user's authorised leads", sum(data["leadStatus"]["counts"]) == my_leads,
              f"{sum(data['leadStatus']['counts'])} vs {my_leads}")
    my_total = sales.api("GET", "/api/customers", params={"pageSize": 1}).json()["totalCount"]
    kpi = re.search(r"Total Customers</div>\s*<div class=\"kpi-value\">(\d+)</div>", page)
    check("14. Total Customers KPI equals the user's authorised customer count", kpi is not None and int(kpi.group(1)) == my_total,
          f"kpi={kpi.group(1) if kpi else None} api={my_total}")
    for period in ["today", "week", "month"]:
        check(f"14. Dashboard date filter '{period}' works", sales.get("/Dashboard", params={"period": period}).status_code == 200)
    r = sales.get("/Dashboard", params={"period": "custom", "from": (today - timedelta(days=30)).isoformat(), "to": today.isoformat()})
    check("14. Dashboard custom date range works", r.status_code == 200 and "–" in r.text)

    # ------------------------------------------------------------------ reports
    for rep in ["Customers", "Leads", "FollowUps", "Opportunities", "Pipeline", "Conversion"]:
        r = sales.get(f"/Reports/{rep}")
        check(f"Report {rep} opens for Sales Executive", r.status_code == 200)
        r = sales.get(f"/Reports/{rep}", params={"export": "csv"})
        check(f"Report {rep} exports CSV", r.status_code == 200 and r.headers.get("Content-Type", "").startswith("text/csv"))
    rows = list(csv.DictReader(io.StringIO(sales.get("/Reports/Customers", params={"export": "csv"}).content.decode("utf-8-sig"))))
    check("Customer report export is scoped to the Sales Executive", rows and all(r["Owner"] == "Rohan Das" for r in rows))
    r = admin.get("/Reports/Opportunities", params={"stage": "Won", "sort": "amount_desc"})
    check("Report filters and sorting work", r.status_code == 200 and "Won" in r.text)
    r = admin.get("/Reports/Leads", params={"page": 2, "pageSize": 5})
    check("Report pagination works", r.status_code == 200 and "Showing 6" in r.text)

    # ------------------------------------------------------------------ list search / filter pages (17.13)
    check("Customers search by phone", "Sanjay Mohanty" in admin.get("/Customers", params={"q": "9437012345"}).text)
    check("Leads filter by status", admin.get("/Leads", params={"status": "Qualified"}).status_code == 200)
    check("Opportunities filter by stage", "Seafood export ERP" in admin.get("/Opportunities", params={"stage": "Negotiation"}).text)
    check("Follow-ups overdue view", "Payment follow-up" in admin.get("/FollowUps", params={"view": "overdue"}).text)
    check("Activities filter by type", admin.get("/Activities", params={"type": "Meeting"}).status_code == 200)

    # ------------------------------------------------------------------ summary
    passed = sum(1 for _, ok, _ in results if ok)
    failed = [(n, d) for n, ok, d in results if not ok]
    print(f"\n{passed}/{len(results)} checks passed")
    if failed:
        print("Failed:")
        for n, d in failed:
            print(f"  - {n} {d}")
    return 0 if not failed else 1


if __name__ == "__main__":
    sys.exit(main())
