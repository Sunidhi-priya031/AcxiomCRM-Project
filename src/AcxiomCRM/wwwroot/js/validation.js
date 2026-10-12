// Client-side adapters for AcxiomCRM's custom validation attributes.
// The same rules are always enforced again on the server.
(function ($) {
    "use strict";
    if (!$ || !$.validator || !$.validator.unobtrusive) return;

    function todayIso() {
        var d = new Date();
        var m = String(d.getMonth() + 1).padStart(2, "0");
        var day = String(d.getDate()).padStart(2, "0");
        return d.getFullYear() + "-" + m + "-" + day;
    }

    function fieldValue(form, name) {
        var checked = $(form).find('[name="' + name + '"]:checked');
        if (checked.length) return String(checked.val()).toLowerCase();
        var el = $(form).find('[name="' + name + '"]').not('[type=hidden]').first();
        if (!el.length) el = $(form).find('[name="' + name + '"]').first();
        if (!el.length) return "";
        if (el.is(":checkbox")) return el.is(":checked") ? "true" : "false";
        return String(el.val() || "").toLowerCase();
    }

    // [NotInPast]: date part must be today or later (optionally skipped for certain values of another field)
    $.validator.addMethod("notinpast", function (value, element, params) {
        if (!value) return true;
        if (params.unlessproperty) {
            var other = fieldValue(element.form, params.unlessproperty);
            var skip = (params.unlessvalues || "").toLowerCase().split(",").map(function (s) { return s.trim(); });
            if (skip.indexOf(other) >= 0) return true;
        }
        // value is yyyy-MM-dd or yyyy-MM-ddTHH:mm; compare the date part as text
        return value.substring(0, 10) >= todayIso();
    });
    $.validator.unobtrusive.adapters.add("notinpast", ["unlessproperty", "unlessvalues"], function (options) {
        options.rules.notinpast = {
            unlessproperty: options.params.unlessproperty,
            unlessvalues: options.params.unlessvalues
        };
        options.messages.notinpast = options.message;
    });

    // [RequiredIf]: required when another field has the target value
    $.validator.addMethod("requiredif", function (value, element, params) {
        if (fieldValue(element.form, params.dependentproperty) !== String(params.targetvalue).toLowerCase()) return true;
        return $.trim(value || "").length > 0;
    });
    $.validator.unobtrusive.adapters.add("requiredif", ["dependentproperty", "targetvalue"], function (options) {
        options.rules.requiredif = {
            dependentproperty: options.params.dependentproperty,
            targetvalue: options.params.targetvalue
        };
        options.messages.requiredif = options.message;
    });

    // Re-validate the dependent date when the stage changes (Won/Lost suspends the close-date rule)
    $(document).on("change", "select, input[type=radio], input[type=checkbox]", function () {
        var form = this.form;
        if (!form || !$(form).data("validator")) return;
        $(form).find("[data-val-notinpast-unlessproperty], [data-val-requiredif]").each(function () {
            if ($(this).val() || $(this).hasClass("input-validation-error")) $(this).valid();
        });
    });
})(window.jQuery);
