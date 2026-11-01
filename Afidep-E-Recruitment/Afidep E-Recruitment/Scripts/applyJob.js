$(document).ready(function () {

    if ($("#isFirstLand").val() === "true") {
        localStorage.removeItem("lastActiveTab");
        $("#applicationTabs button:not(:first)").prop("disabled", true);
    }

    var applicationTabs = $("#applicationTabs");

    applicationTabs.on("shown.bs.tab", function (e) {
        var activeTabId = $(e.target).attr("id");
        localStorage.setItem("lastActiveTab", activeTabId);
    });

    var lastActiveTabId = localStorage.getItem("lastActiveTab");
    if (lastActiveTabId) {
        var lastActiveTabButton = $("#" + lastActiveTabId);
        if (lastActiveTabButton.length) {
            $("#applicationTabs .nav-link.active").removeClass("active");
            $(".tab-pane.show.active").removeClass("show active");
            var bsTabRestore = new bootstrap.Tab(lastActiveTabButton[0]);
            bsTabRestore.show();
            $("html, body").scrollTop(0);
        } else {
            localStorage.removeItem("lastActiveTab");
            new bootstrap.Tab($("#basic-info-tab")[0]).show();
        }
    } else {
        new bootstrap.Tab($("#basic-info-tab")[0]).show();
    }

    var specialNeedsDropdown = $("#personalinfo_specialneeds");
    var disabilityDetailsField = $("#personalinfo_disabilitydetails");

    function toggleDisabilityDetails() {
        if (specialNeedsDropdown.val() === "Yes") {
            disabilityDetailsField.prop("readonly", false);
        } else {
            disabilityDetailsField.prop("readonly", true);
            disabilityDetailsField.val("");
        }
    }

    toggleDisabilityDetails();
    specialNeedsDropdown.on("change", toggleDisabilityDetails);

    function validateTab(tabId) {
        var isValid = true;
        $(tabId).find(":input[required]:visible").each(function () {
            if (!this.checkValidity()) {
                isValid = false;
                $(this).addClass("is-invalid");
                var name = $(this).attr("name");
                var span = $(this).nextAll(".text-danger[data-valmsg-for='" + name + "']");
                if (span.length === 0) {
                    $(this).after("<span class='text-danger field-validation-error' data-valmsg-for='" + name + "'>This field is required.</span>");
                } else {
                    span.text(this.validationMessage);
                }
            } else {
                $(this).removeClass("is-invalid");
                $(this).nextAll(".text-danger[data-valmsg-for='" + $(this).attr("name") + "']").text("");
            }
        });
        return isValid;
    }

    function enableTab(tabButtonJqueryObject) {
        tabButtonJqueryObject.prop("disabled", false);
    }

    function RelocatePage(applicationNumber) {
        setTimeout(function () {
            window.location.href = "/Job/ApplyJob?applicationnumber=" + applicationNumber;
        }, 1000);
    }

    function spinnerHtml(message) {
        return "<div class='modal-body text-center py-5'><div class='spinner-border text-primary' role='status'><span class='visually-hidden'>Loading...</span></div><p class='mt-2'>" + message + "</p></div>";
    }

    function showModal(modal, modalContent, message) {
        modalContent.html(spinnerHtml(message));
        var bsModal = new bootstrap.Modal(modal[0]);
        bsModal.show();
        return bsModal;
    }

    $("#ProfileTabNextButton").on("click", function () {
        var currentTabPane = $(this).closest(".tab-pane");
        var currentTabId = "#" + currentTabPane.attr("id");
        var allTabButtons = $("#applicationTabs button");
        var currentTabButton = $("#applicationTabs button[data-bs-target='" + currentTabId + "']");
        var currentTabButtonIndex = allTabButtons.index(currentTabButton);
        var modal = $("#academicQualificationModal");
        var modalContent = modal.find("#academicQualificationModalContent");

        if (validateTab(currentTabId)) {
            var bsModal = showModal(modal, modalContent, "Loading update...");

            var updateinputjson = {
                applicationnumber: $("#applicationnumber").val(),
                modify_type: "user_details",
                application_details: {
                    personalinfo: {
                        firstname: $("#personalinfo_firstname").val(),
                        middlename: $("#personalinfo_middlename").val(),
                        lastname: $("#personalinfo_lastname").val(),
                        email: $("#personalinfo_email").val(),
                        gender: $("#personalinfo_gender option:selected").val(),
                        citizenship: $("#personalinfo_citizenship option:selected").val(),
                        specialneeds: $("#personalinfo_specialneeds option:selected").val(),
                        telephone: $("#personalinfo_telephone").val(),
                        disabilitydetails: $("#personalinfo_disabilitydetails").val()
                    }
                }
            };

            $.ajax({
                type: "POST",
                url: "/Job/EditJobApp",
                contentType: "application/json; charset=utf-8",
                dataType: "json",
                data: JSON.stringify(updateinputjson),
                complete: function () {
                    setTimeout(function () {
                        bsModal.hide();
                        setTimeout(function () {
                            modal.hide();
                            $(".modal-backdrop").hide();
                            $("body").removeClass("modal-open");
                        }, 50);
                    }, 300);
                }
            });

            if (currentTabButtonIndex === 0) {
                allTabButtons.slice(currentTabButtonIndex + 1).each(function () { enableTab($(this)); });
            } else {
                var nb = currentTabButton.parent().next().find("button");
                if (nb.length) { enableTab(nb); }
            }

            var ns = currentTabButton.parent().next().find("button");
            if (ns.length) { new bootstrap.Tab(ns[0]).show(); }

        } else {
            $(currentTabId).find(":input[required]:invalid:visible").first().focus();
        }
    });

    $(".next-tab").on("click", function () {
        var currentTabPane = $(this).closest(".tab-pane");
        var currentTabId = "#" + currentTabPane.attr("id");
        var allTabButtons = $("#applicationTabs button");
        var currentTabButton = $("#applicationTabs button[data-bs-target='" + currentTabId + "']");
        var currentTabButtonIndex = allTabButtons.index(currentTabButton);
        var sectionError = "";

        if (currentTabId === "#academic" && $("#academicQualificationsContainer tbody tr").length === 0) {
            sectionError = "Please add at least one Academic Qualification before proceeding.";
        }
        if (currentTabId === "#experience" && $("#experiencesContainer tbody tr").length === 0) {
            sectionError = "Please add at least one Work Experience before proceeding.";
        }
        if (currentTabId === "#attachments") {
            var requiredDocs = ["Curriculum Vitae", "Cover Letter"];
            var uploadedTypes = [];
            $("#AttachmentsContainer tbody tr").each(function () {
                uploadedTypes.push($(this).find("td:first").text().trim());
            });
            var missingDocs = requiredDocs.filter(function (doc) { return uploadedTypes.indexOf(doc) === -1; });
            if (missingDocs.length > 0) {
                sectionError = "Please upload the following required documents: " + missingDocs.join(", ");
            }
        }

        var existingAlert = currentTabPane.find(".section-alert");
        if (sectionError !== "") {
            if (existingAlert.length === 0) {
                currentTabPane.find(".card").prepend("<div class='alert alert-danger section-alert'>" + sectionError + "</div>");
            } else {
                existingAlert.text(sectionError).show();
            }
            return;
        } else {
            existingAlert.hide();
        }

        if (validateTab(currentTabId)) {
            if (currentTabButtonIndex === 0) {
                allTabButtons.slice(currentTabButtonIndex + 1).each(function () { enableTab($(this)); });
            } else {
                var nb = currentTabButton.parent().next().find("button");
                if (nb.length) { enableTab(nb); }
            }
            var ns = currentTabButton.parent().next().find("button");
            if (ns.length) { new bootstrap.Tab(ns[0]).show(); }
        } else {
            $(currentTabId).find(":input[required]:invalid:visible").first().focus();
        }
    });

    $(".prev-tab").on("click", function () {
        var currentTabPane = $(this).closest(".tab-pane");
        var currentTabId = "#" + currentTabPane.attr("id");
        var currentTabButton = $("#applicationTabs button[data-bs-target='" + currentTabId + "']");
        var prevTabButton = currentTabButton.parent().prev().find("button");
        if (prevTabButton.length) { new bootstrap.Tab(prevTabButton[0]).show(); }
    });

    function openModalWithAjax(url, applicationNumber) {
        var modal = $("#academicQualificationModal");
        var modalContent = modal.find("#academicQualificationModalContent");
        showModal(modal, modalContent, "Loading form...");
        $.ajax({
            url: url,
            type: "GET",
            data: { applicationNumber: applicationNumber },
            success: function (data) { modalContent.html(data); },
            error: function (xhr, status, error) {
                modalContent.html("<div class='alert alert-danger'>Error loading form: " + error + "</div>");
            }
        });
    }

    $("#addAcademicQualButton").on("click", function () {
        openModalWithAjax("/Job/AddAcademicQualification", $(this).data("application-number"));
    });
    $("#addWorkExpButton").on("click", function () {
        openModalWithAjax("/Job/AddWorkExperience", $(this).data("application-number"));
    });
    $("#addProfCourse").on("click", function () {
        openModalWithAjax("/Job/AddProfessionalCourse", $(this).data("application-number"));
    });
    $("#addProfMemb").on("click", function () {
        openModalWithAjax("/Job/AddProfessionalMembership", $(this).data("application-number"));
    });
    $("#addReferee").on("click", function () {
        openModalWithAjax("/Job/AddReferee", $(this).data("application-number"));
    });
    $("#addAttachments").on("click", function () {
        openModalWithAjax("/Job/AddAttachment", $(this).data("application-number"));
    });

    $(document).on("click", ".remove-row", function () {
        if (confirm("Are you sure you want to remove this entry?")) {
            $(this).closest(".form-row-item").remove();
        }
    });

    $("#SaveProfileInfo").on("click", function () {
        var modal = $("#academicQualificationModal");
        var modalContent = modal.find("#academicQualificationModalContent");
        showModal(modal, modalContent, "Saving...");

        var updateinputjson = {
            applicationnumber: $("#applicationnumber").val(),
            modify_type: "user_details",
            application_details: {
                personalinfo: {
                    firstname: $("#personalinfo_firstname").val(),
                    middlename: $("#personalinfo_middlename").val(),
                    lastname: $("#personalinfo_lastname").val(),
                    email: $("#personalinfo_email").val(),
                    gender: $("#personalinfo_gender option:selected").val(),
                    citizenship: $("#personalinfo_citizenship option:selected").val(),
                    specialneeds: $("#personalinfo_specialneeds option:selected").val(),
                    telephone: $("#personalinfo_telephone").val(),
                    disabilitydetails: $("#personalinfo_disabilitydetails").val()
                }
            }
        };

        $.ajax({
            type: "POST",
            url: "/Job/EditJobApp",
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            data: JSON.stringify(updateinputjson),
            success: function () { RelocatePage(updateinputjson.applicationnumber); },
            error: function () { RelocatePage(updateinputjson.applicationnumber); }
        });
    });

    var pdfLoaded = false;

    $("#preview-tab").on("shown.bs.tab", function () {
        if (!pdfLoaded) {
            var pdfViewer = $("#pdfViewer");
            var pdfPlaceholder = $("#pdfPlaceholder");
            var applicationNumber = $("#applicationnumber").val();

            pdfPlaceholder.show();
            pdfViewer.hide();

            if (applicationNumber) {
                $.ajax({
                    url: "/Job/GetApplicationPdfBase64",
                    type: "GET",
                    data: { applicationNumber: applicationNumber },
                    success: function (base64String) {
                        if (base64String) {
                            pdfViewer.attr("src", "data:application/pdf;base64," + base64String);
                            pdfViewer.on("load", function () {
                                pdfPlaceholder.hide();
                                pdfViewer.show();
                                pdfLoaded = true;
                            }).on("error", function () {
                                pdfPlaceholder.html("<p class='text-danger'>Failed to load PDF preview.</p>");
                                pdfViewer.hide();
                            });
                        } else {
                            pdfPlaceholder.html("<p class='text-info'>No PDF content available for this application.</p>");
                            pdfViewer.hide();
                        }
                    },
                    error: function (xhr, status, error) {
                        pdfPlaceholder.html("<p class='text-danger'>Error loading PDF: " + (xhr.responseText || error) + "</p>");
                        pdfViewer.hide();
                    }
                });
            } else {
                pdfPlaceholder.html("<p class='text-warning'>Application number not available to load PDF.</p>");
                pdfViewer.hide();
            }
        }
    });

    $("#submitApplicationButton").prop("disabled", true);

    $("#declarationAgreeCheck").on("change", function () {
        if ($(this).is(":checked")) {
            $("#submitApplicationButton").prop("disabled", false);
            $("#declarationWarning").hide();
        } else {
            $("#submitApplicationButton").prop("disabled", true);
        }
    });

});
