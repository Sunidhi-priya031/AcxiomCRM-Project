#!/usr/bin/env python3
"""
Browser checks for AcxiomCRM (client-side validation, confirmation dialogs, Chart.js dashboard).

Usage:
    pip install playwright && playwright install chromium
    python tests/browser_test.py --base-url http://localhost:5173 --password "<Seed:DemoPassword>" [--screenshots out/]
"""
import argparse
import os
import sys
from datetime import date, timedelta

from playwright.sync_api import sync_playwright

results = []


def check(name, condition, detail=""):
    results.append((name, bool(condition)))
    print(f"[{'PASS' if condition else 'FAIL'}] {name}" + (f"  -- {detail}" if detail and not condition else ""))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base-url", default="http://localhost:5173")
    ap.add_argument("--password", required=True)
    ap.add_argument("--screenshots", default=None)
    args = ap.parse_args()
    base = args.base_url.rstrip("/")
    shots = args.screenshots
    if shots:
        os.makedirs(shots, exist_ok=True)

    def snap(page, name):
        if shots:
            page.screenshot(path=os.path.join(shots, name + ".png"), full_page=True)

    with sync_playwright() as p:
        browser = p.chromium.launch()
        ctx = browser.new_context(viewport={"width": 1440, "height": 900})
        page = ctx.new_page()
        errors = []
        page.on("pageerror", lambda e: errors.append(str(e)))

        page.goto(base + "/Account/Login")
        snap(page, "01-login")
        # Client-side validation on login
        page.click("button[type=submit]")
        check("Login form blocks empty submission client-side", "/Account/Login" in page.url and page.locator("text=Email is required.").is_visible())

        page.fill("#Email", "rohan.das@acxiomcrm.local")
        page.fill("#Password", args.password)
        page.click("button[type=submit]")
        page.wait_for_load_state("networkidle")
        check("Login lands on the dashboard", page.url.rstrip("/") == base or "/Dashboard" in page.url, page.url)

        # Dashboard charts
        page.wait_for_timeout(800)
        charts = page.evaluate("() => ['leadStatusChart','pipelineChart','monthlyChart'].map(id => !!(window.Chart && Chart.getChart(document.getElementById(id))))")
        check("Chart.js renders the three dashboard charts", all(charts), str(charts))
        snap(page, "02-dashboard-sales")

        # Scenario 3: client-side validation blocks invalid customer
        page.goto(base + "/Customers/Create")
        page.fill("#CustomerName", "Client Validation Test")
        page.fill("#Email", "invalid-email")
        page.fill("#Phone", "12345")
        page.click(".form-actions button[type=submit]")
        page.wait_for_timeout(500)
        check("3. Invalid email shows client-side message", page.locator("text=Enter a valid email address.").is_visible())
        check("3. Invalid phone shows client-side message", page.locator("text=Enter a valid phone number.").is_visible())
        check("3. Save is blocked (still on the create page)", page.url.endswith("/Customers/Create"))
        snap(page, "03-customer-client-validation")

        # Remote duplicate check
        page.fill("#Email", "sanjay.mohanty@kalingasteel.in")
        page.fill("#Phone", "9437012345")
        page.locator("#CustomerName").click()
        page.wait_for_timeout(800)
        check("Duplicate email flagged before submit (remote validation)", page.locator("text=A customer with this email already exists.").is_visible())

        # Opportunity: amount, probability and close date client rules
        page.goto(base + "/Opportunities/Create")
        page.select_option("#CustomerId", index=1)
        page.fill("#OpportunityName", "Client Rules Test")
        page.fill("#Amount", "0")
        page.fill("#Probability", "101")
        page.fill("#ExpectedCloseDate", (date.today() - timedelta(days=1)).isoformat())
        page.click(".form-actions button[type=submit]")
        page.wait_for_timeout(500)
        check("5. Amount = 0 blocked client-side", page.locator("text=Opportunity Amount must be greater than 0.").first.is_visible())
        check("6. Probability > 100 blocked client-side", page.locator("text=Probability must be between 0 and 100.").first.is_visible())
        check("7. Past close date blocked client-side", page.locator("text=Expected Close Date cannot be in the past.").first.is_visible())
        check("Opportunity save blocked", page.url.endswith("/Opportunities/Create"))
        snap(page, "04-opportunity-client-validation")

        # Follow-up date rule client-side
        page.goto(base + "/FollowUps/Create")
        page.select_option("#CustomerId", index=1)
        page.fill("#Subject", "Client date test")
        page.fill("#FollowUpDate", (date.today() - timedelta(days=1)).isoformat() + "T10:00")
        page.click(".form-actions button[type=submit]")
        page.wait_for_timeout(500)
        check("8. Past follow-up date blocked client-side", page.locator("text=Follow-up date cannot be earlier than today.").first.is_visible())
        page.check("#rel-Lead")
        check("Related-record selector switches to Lead", page.locator("#LeadId").is_visible() and not page.locator("#CustomerId").is_visible())
        snap(page, "05-followup-form")

        # Lead conversion form: conditional required rules
        page.goto(base + "/Leads?status=Qualified")
        snap(page, "06-leads-list")

        page.goto(base + "/Opportunities/Pipeline")
        snap(page, "07-pipeline")

        # Confirmation dialog on destructive action (manager can delete)
        ctx2 = browser.new_context(viewport={"width": 1440, "height": 900})
        mpage = ctx2.new_page()
        mpage.on("pageerror", lambda e: errors.append(str(e)))
        mpage.goto(base + "/Account/Login")
        mpage.fill("#Email", "manager@acxiomcrm.local")
        mpage.fill("#Password", args.password)
        mpage.click("button[type=submit]")
        mpage.wait_for_load_state("networkidle")
        mpage.wait_for_timeout(600)
        snap(mpage, "08-dashboard-manager")
        mpage.goto(base + "/Customers")
        mpage.locator("a[aria-label^='Delete']").first.click()
        mpage.wait_for_load_state("networkidle")
        delete_url = mpage.url
        mpage.locator(".form-actions button.btn-danger").click()
        mpage.wait_for_timeout(500)
        check("Destructive action asks for confirmation", mpage.locator("#confirmModal").is_visible())
        snap(mpage, "09-confirm-dialog")
        mpage.locator("#confirmModal button[data-bs-dismiss=modal]").first.click()
        mpage.wait_for_timeout(400)
        check("Cancelling the confirmation keeps the record", mpage.url == delete_url)

        # Opportunity close with confirmation keeps the clicked outcome
        mpage.goto(base + "/Opportunities?status=Open")
        mpage.locator("table a.fw-medium").first.click()
        mpage.wait_for_load_state("networkidle")
        mpage.locator("button[value=Lost]").click()
        mpage.wait_for_timeout(400)
        mpage.locator("#confirmModalOk").click()
        mpage.wait_for_load_state("networkidle")
        check("Mark Lost via confirmation dialog posts the chosen outcome", mpage.locator(".alert-success").filter(has_text="Opportunity marked Lost.").is_visible())

        # Admin pages
        ctx3 = browser.new_context(viewport={"width": 1440, "height": 900})
        apage = ctx3.new_page()
        apage.on("pageerror", lambda e: errors.append(str(e)))
        apage.goto(base + "/Account/Login")
        apage.fill("#Email", "admin@acxiomcrm.local")
        apage.fill("#Password", args.password)
        apage.click("button[type=submit]")
        apage.wait_for_load_state("networkidle")
        apage.wait_for_timeout(600)
        snap(apage, "10-dashboard-admin")
        for path, name in [("/Customers", "11-customers"), ("/Customers/Details/1", "12-customer-details"), ("/Users", "13-users"),
                           ("/Roles", "14-roles"), ("/AuditLogs", "15-audit"), ("/Reports", "16-reports"),
                           ("/Reports/Pipeline", "17-report-pipeline"), ("/FollowUps", "18-followups"), ("/Activities", "19-activities"),
                           ("/Leads/Details/3", "20-lead-details")]:
            apage.goto(base + path)
            snap(apage, name)
        apage.goto(base + "/Leads/Convert/3")
        apage.check("#existingCustomer")
        check("Conversion form shows existing-customer picker", apage.locator("#ExistingCustomerId").is_visible())
        apage.click(".form-actions button[type=submit]")
        apage.wait_for_timeout(400)
        check("Conversion requires a customer when linking to existing (client-side)", apage.locator("text=Select an existing customer.").is_visible())
        snap(apage, "21-lead-convert")

        # Tablet layout
        tctx = browser.new_context(viewport={"width": 820, "height": 1180})
        tpage = tctx.new_page()
        tpage.goto(base + "/Account/Login")
        tpage.fill("#Email", "admin@acxiomcrm.local")
        tpage.fill("#Password", args.password)
        tpage.click("button[type=submit]")
        tpage.wait_for_load_state("networkidle")
        check("Tablet: sidebar collapses behind a toggle", not tpage.locator("#sidebar").is_visible() or tpage.locator("#sidebarToggle").is_visible())
        tpage.click("#sidebarToggle")
        tpage.wait_for_timeout(400)
        check("Tablet: toggle opens the navigation", tpage.locator("#sidebar .nav-item").first.is_visible())
        snap(tpage, "22-tablet-nav")

        check("No JavaScript errors on any page", not errors, "; ".join(errors[:3]))
        browser.close()

    passed = sum(1 for _, ok in results if ok)
    print(f"\n{passed}/{len(results)} browser checks passed")
    return 0 if passed == len(results) else 1


if __name__ == "__main__":
    sys.exit(main())
