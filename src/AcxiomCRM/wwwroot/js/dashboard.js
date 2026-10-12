// Dashboard charts (Chart.js). Data is rendered by the server from authorised records only.
(function () {
    "use strict";
    var el = document.getElementById("dashboardData");
    if (!el || !window.Chart) return;
    var data = JSON.parse(el.textContent);

    var rupee = new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR", maximumFractionDigits: 0 });
    var compact = new Intl.NumberFormat("en-IN", { notation: "compact", maximumFractionDigits: 1 });

    Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
    Chart.defaults.font.size = 12;
    Chart.defaults.color = "#4b5563";
    Chart.defaults.plugins.legend.labels.boxWidth = 12;
    Chart.defaults.maintainAspectRatio = false;

    var statusColors = {
        New: "#2f6fde", Contacted: "#0f8fb3", Qualified: "#6a5acd", Unqualified: "#9ca3af",
        Converted: "#1e9e5a", Lost: "#d64545",
        Qualification: "#0f8fb3", Proposal: "#2f6fde", Negotiation: "#d98c1a", Won: "#1e9e5a"
    };
    var colorsFor = function (labels) { return labels.map(function (l) { return statusColors[l] || "#9ca3af"; }); };

    // Lead status
    new Chart(document.getElementById("leadStatusChart"), {
        type: "doughnut",
        data: {
            labels: data.leadStatus.labels,
            datasets: [{ data: data.leadStatus.counts, backgroundColor: colorsFor(data.leadStatus.labels), borderWidth: 2, borderColor: "#fff" }]
        },
        options: {
            cutout: "62%",
            plugins: { legend: { position: "right" } }
        }
    });

    // Opportunity pipeline by stage
    new Chart(document.getElementById("pipelineChart"), {
        type: "bar",
        data: {
            labels: data.pipeline.labels,
            datasets: [{
                label: "Opportunities",
                data: data.pipeline.counts,
                backgroundColor: colorsFor(data.pipeline.labels),
                borderRadius: 4,
                maxBarThickness: 42
            }]
        },
        options: {
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        afterLabel: function (ctx) { return "Amount: " + rupee.format(data.pipeline.amounts[ctx.dataIndex]); }
                    }
                }
            },
            scales: {
                y: { beginAtZero: true, ticks: { precision: 0 }, grid: { color: "#eef1f5" } },
                x: { grid: { display: false } }
            }
        }
    });

    // Monthly sales: won amount (bars) + won/lost counts (lines)
    new Chart(document.getElementById("monthlyChart"), {
        data: {
            labels: data.monthly.labels.map(function (l) { return l.substring(0, 3) + " " + l.substring(6); }),
            datasets: [
                { type: "bar", label: "Won amount", data: data.monthly.wonAmount, backgroundColor: "rgba(47,111,222,.75)", borderRadius: 3, yAxisID: "y", order: 2 },
                { type: "line", label: "Won", data: data.monthly.won, borderColor: "#1e9e5a", backgroundColor: "#1e9e5a", tension: .3, yAxisID: "y1", order: 1, pointRadius: 3 },
                { type: "line", label: "Lost", data: data.monthly.lost, borderColor: "#d64545", backgroundColor: "#d64545", tension: .3, yAxisID: "y1", order: 1, pointRadius: 3 }
            ]
        },
        options: {
            interaction: { mode: "index", intersect: false },
            plugins: {
                legend: { position: "bottom" },
                tooltip: {
                    callbacks: {
                        label: function (ctx) {
                            return ctx.dataset.yAxisID === "y" ? ctx.dataset.label + ": " + rupee.format(ctx.parsed.y) : ctx.dataset.label + ": " + ctx.parsed.y;
                        }
                    }
                }
            },
            scales: {
                y: { beginAtZero: true, position: "left", ticks: { callback: function (v) { return "₹" + compact.format(v); } }, grid: { color: "#eef1f5" } },
                y1: { beginAtZero: true, position: "right", ticks: { precision: 0 }, grid: { display: false } },
                x: { grid: { display: false } }
            }
        }
    });
})();
