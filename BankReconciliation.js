(function (window, $) {

    "use strict";

    var BankReconciliation = {

        state: {
            reconciliationId: 0,
            page: 1,
            pageSize: 10,
            status: null,
            search: "",
            selectedBankTransactionId: null,
            selectedAccountingDetailId: null,
            selectedBankTransactionIds: [],
            selectedAccountingDetailIds: [],
            candidateMode: 1,
            totalCount: 0,
            totalPages: 0,
            reconciliationState: 0,
            bankTable: null,
            candidateTable: null
        },

        init: function () {

            this.initBankAccount();
            this.initDates();
            this.initTables();
            this.bindEvents();
            this.updateButtons();

        },

        initBankAccount: function () {

            $("#BankAccountId").select2({
                dir: "rtl",
                language: select2_face,
                dropdownAutoWidth: true,
                width: "100%",
                ajax: {
                    url: "/BankAccount/GetFullSelect",
                    headers: {
                        "RequestVerificationToken": $("#forgeryToken").val()
                    },
                    dataType: "json",
                    type: "POST",
                    processResults: function (data, params) {

                        params.page = params.page || 1;

                        return {
                            results: data.results,
                            pagination: data.pagination
                        };

                    },
                    data: function (params) {

                        return {
                            search: params.term,
                            page: params.page || 1,
                            pageCount: 10
                        };

                    }
                }
            });

        },

        initDates: function () {

            $("#FromDate").MdPersianDateTimePicker({
                targetTextSelector: "#FromDate",
                enableTimePicker: false,
                textFormat: "yyyy/MM/dd",
                isGregorian: false
            });

            $("#ToDate").MdPersianDateTimePicker({
                targetTextSelector: "#ToDate",
                enableTimePicker: false,
                textFormat: "yyyy/MM/dd",
                isGregorian: false
            });

        },

        initTables: function () {

            var self = this;

            this.state.bankTable = $("#bankTransactions").DataTable({
                processing: true,
                serverSide: true,
                searching: false,
                ordering: false,
                lengthChange: false,
                pageLength: this.state.pageSize,
                pagingType: "simple_numbers",
                dom: "rt<'row'<'col'l><'col'p><'col'i>>",
                select: {
                    style: "single",
                    selector: 'td:not(:last-child)'
                },
                rowId: "Id",
                language: dataTablesCurrentLanguage,
                ajax: function (data, callback) {

                    if (!self.state.reconciliationId) {

                        callback({
                            draw: data.draw,
                            recordsTotal: 0,
                            recordsFiltered: 0,
                            data: []
                        });

                        return;
                    }

                    self.state.page =
                        Math.floor(
                            data.start / data.length
                        ) + 1;

                    self.state.pageSize =
                        data.length;

                    $.ajax({

                        url: "/BankReconciliation/GetPage",

                        type: "POST",
                        headers: {
                            "RequestVerificationToken": $("#forgeryToken").val()
                        },

                        data: JSON.stringify({

                            reconciliationId:
                                self.state.reconciliationId,

                            filter: {

                                Search:
                                    self.state.search || null,

                                Status:
                                    self.state.status,

                                Page:
                                    self.state.page,

                                PageSize:
                                    self.state.pageSize

                            }

                        }),

                        contentType:
                            "application/json; charset=utf-8",

                        dataType:
                            "json"

                    })
                        .done(function (response) {

                            if (!self.handleResponse(response)) {

                                callback({
                                    draw: data.draw,
                                    recordsTotal: 0,
                                    recordsFiltered: 0,
                                    data: []
                                });

                                return;
                            }

                            var result =
                                response.Result;

                            self.renderSummary(
                                result.Summary
                            );

                            self.state.totalCount =
                                self.number(
                                    result.TotalCount
                                );

                            self.state.totalPages =
                                Math.ceil(
                                    self.state.totalCount /
                                    self.state.pageSize
                                );

                            self.state.reconciliationState =
                                Number(
                                    result.Reconciliation.State
                                );

                            self.updateButtons();

                            callback({
                                draw: data.draw,
                                recordsTotal:
                                    result.TotalCount,
                                recordsFiltered:
                                    result.TotalCount,
                                data:
                                    result.Transactions || []
                            });

                        })
                        .fail(function () {

                            self.showError(
                                "خطا در دریافت تراکنش‌های بانکی."
                            );

                            callback({
                                draw: data.draw,
                                recordsTotal: 0,
                                recordsFiltered: 0,
                                data: []
                            });

                        });

                },

                columns: [

                    {
                        data: null,
                        className: "text-center",
                        width: "45px",
                        render: function () {

                            return "";

                        }
                    },

                    {
                        data: "TransactionDate",
                        className: "text-center",
                        width: "95px",
                        render: function (data, type, row) {

                            return self.escapeHtml(
                                self.formatDate(data)
                            ) +
                            '<div class="br-table-time">' +
                            self.escapeHtml(
                                row.TransactionTime || ""
                            ) +
                            "</div>";

                        }
                    },

                    {
                        data: "Description",
                        render: function (data, type, row) {

                            return '<div class="br-description">' +
                                self.escapeHtml(
                                    data || ""
                                ) +
                                "</div>";

                        }
                    },

                    {
                        data: null,
                        className: "text-center",
                        width: "75px",
                        render: function (data, type, row) {

                            return self.number(row.Credit) > 0
                                ? "واریز"
                                : "برداشت";

                        }
                    },

                    {
                        data: "TrackingNumber",
                        className: "text-center",
                        width: "130px",
                        render: function (data) {

                            return self.escapeHtml(
                                data || "-"
                            );

                        }
                    },

                    {
                        data: "Amount",
                        className: "text-left",
                        width: "130px",
                        render: function (data, type, row) {

                            var value =
                                self.formatAmount(data);

                            var cls =
                                self.number(row.Credit) > 0
                                    ? "br-credit"
                                    : "br-debit";

                            return '<span class="' +
                                cls +
                                '">' +
                                value +
                                "</span>";

                        }
                    },

                    {
                        data: "Balance",
                        className: "text-left",
                        width: "140px",
                        render: function (data) {

                            return self.formatAmount(data);

                        }
                    },

                    {
                        data: "RemainingAmount",
                        className: "text-left",
                        width: "140px",
                        render: function (data) {

                            return self.formatAmount(data);

                        }
                    },

                    {
                        data: "Status",
                        className: "text-center",
                        width: "95px",
                        render: function (data) {

                            return self.statusBadge(data);

                        }
                    }

                ],

                rowCallback: function (row, data) {

                    $(row)
                        .attr("data-id", data.Id);

                },

                drawCallback: function () {

                    $("#bankCount")
                        .text(
                            self.formatNumber(
                                self.state.totalCount
                            )
                        );

                }

            });

            this.state.bankTable.on("draw.dt", function () {
                var pageInfo = self.state.bankTable.page.info();
                self.state.bankTable
                    .column(0, { page: "current" })
                    .nodes()
                    .each(function (cell, i) {
                        cell.innerHTML = i + 1 + pageInfo.start;
                    });
            });

            this.state.candidateTable =
                $("#accountingCandidates").DataTable({

                    processing: true,
                    serverSide: true,
                    searching: false,
                    ordering: false,
                    lengthChange: false,
                    pageLength: 10,
                    pagingType: "simple_numbers",
                    autoWidth: false,
                    scrollX: false,

                    dom: "rt<'row'<'col'l><'col'p><'col'i>>",
                    select: {
                        style: "single",
                        selector: 'td:not(:last-child)'
                    },
                    rowId: "AccountingDocDetailId",
                    language: dataTablesCurrentLanguage,

                    ajax: function (data, callback) {

                        if (
                            !self.state.reconciliationId ||
                            !self.state.selectedBankTransactionId
                        ) {

                            callback({
                                draw: data.draw,
                                recordsTotal: 0,
                                recordsFiltered: 0,
                                data: []
                            });

                            return;
                        }

                        var page =
                            Math.floor(
                                data.start / data.length
                            ) + 1;

                        $.ajax({

                            url:
                                "/BankReconciliation/GetCandidates",

                            type: "POST",
                            headers: {
                                "RequestVerificationToken": $("#forgeryToken").val()
                            },

                            data: JSON.stringify({

                                reconciliationId:
                                    self.state.reconciliationId,

                                bankTransactionId:
                                    self.state.selectedBankTransactionId,

                                filter: {

                                    Search: null,

                                    TrackingMatched: null,

                                    Mode:
                                        Number(self.state.candidateMode) || 1,

                                    Page:
                                        page,

                                    PageSize:
                                        data.length

                                }

                            }),

                            contentType:
                                "application/json; charset=utf-8",

                            dataType:
                                "json"

                        })
                            .done(function (response) {

                                if (
                                    !self.handleResponse(response)
                                ) {

                                    callback({
                                        draw: data.draw,
                                        recordsTotal: 0,
                                        recordsFiltered: 0,
                                        data: []
                                    });

                                    return;
                                }

                                var result =
                                    response.Result;

                                callback({

                                    draw: data.draw,

                                    recordsTotal:
                                        result.TotalCount,

                                    recordsFiltered:
                                        result.TotalCount,

                                    data:
                                        result.Candidates || []

                                });

                                $("#candidateCount")
                                    .text(
                                        self.formatNumber(
                                            result.TotalCount
                                        )
                                    );

                            })
                            .fail(function () {

                                self.showError(
                                    "خطا در دریافت اسناد قابل تطبیق."
                                );

                                callback({
                                    draw: data.draw,
                                    recordsTotal: 0,
                                    recordsFiltered: 0,
                                    data: []
                                });

                            });

                    },

                    columns: [

                        {
                            data: null,
                            className: "text-center",
                            width: "6%",
                            render: function () {

                                return "";

                            }
                        },

                        {
                            data: "AccountingDocDate",
                            className: "text-center",
                            width: "8%",
                            render: function (data) {

                                return self.escapeHtml(
                                    self.formatDate(data)
                                );

                            }
                        },

                        {
                            data: "AccountingDocId",
                            className: "text-center",
                            width: "8%",
                            render: function (data) {

                                return self.formatNumber(data);

                            }
                        },

                        {
                            data: "AccountTitle",
                            width: "14%",
                            render: function (data) {

                                return self.escapeHtml(
                                    data || ""
                                );

                            }
                        },

                        {
                            data: "Description",
                            width: "27%",
                            render: function (data) {

                                return '<div class="br-description">' +
                                    self.escapeHtml(
                                        data || ""
                                    ) +
                                    "</div>";

                            }
                        },

                        {
                            data: "TrackingSource",
                            className: "text-center",
                            width: "9%",
                            render: function (data) {

                                if (data === "DepositSlip") {
                                    return "فیش واریز";
                                }

                                if (data === "Cheque") {
                                    return "چک";
                                }

                                if (data === "DepositSlip, Cheque") {
                                    return "فیش واریز / چک";
                                }

                                return "-";

                            }
                        },

                        {
                            data: "TrackingNumber",
                            className: "text-center",
                            width: "11%",
                            render: function (data) {

                                return self.escapeHtml(
                                    data || "-"
                                );

                            }
                        },

                        {
                            data: "Amount",
                            className: "text-left",
                            width: "11%",
                            render: function (data) {

                                return self.formatAmount(data);

                            }
                        },
{
                            data: "AmountDifference",
                            className: "text-left",
                            width: "10%",
                            render: function (data) {

                                return self.formatAmount(data);

                            }
                        }

                    ],

                    rowCallback: function (row, data) {

                        $(row)
                            .attr(
                                "data-id",
                                data.AccountingDocDetailId
                            );

                    }

                });

            this.state.candidateTable.on("draw.dt", function () {
                var pageInfo = self.state.candidateTable.page.info();
                self.state.candidateTable
                    .column(0, { page: "current" })
                    .nodes()
                    .each(function (cell, i) {
                        cell.innerHTML = i + 1 + pageInfo.start;
                    });
            });

        },

        bindEvents: function () {

            var self = this;

            $(".br-tab").on("click", function () {

                $(".br-tab")
                    .removeClass("active");

                $(this)
                    .addClass("active");

                var status =
                    $(this).data("status");

                if (status === "all") {
                    self.state.status = null;
                }
                else if (status === "partial") {
                    self.state.status = 1;
                }
                else if (status === "unmatched") {
                    self.state.status = 0;
                }
                else if (status === "matched") {
                    self.state.status = 2;
                }

                self.reloadBankTable();

            });

            self.state.bankTable.on("select.dt", function (e, dt, type, indexes) {

                if (type !== "row" || !indexes.length) {
                    return;
                }

                var data = dt.row(indexes[0]).data();

                if (!data) {
                    return;
                }

                self.selectBankTransaction(data.Id);

            });

            self.state.candidateTable.on("select.dt", function (e, dt, type, indexes) {

                if (type !== "row" || !indexes.length) {
                    return;
                }

                var data = dt.row(indexes[0]).data();

                if (!data) {
                    return;
                }

                self.selectAccountingDetail(data);

            });

            $("#txtSearch").on("keyup", function () {

                clearTimeout(self.searchTimer);

                self.searchTimer =
                    setTimeout(function () {

                        self.state.search =
                            $("#txtSearch").val();

                        self.reloadBankTable();

                    }, 400);

            });

            $("#btnLoad").on("click", function () {

                self.state.reconciliationId = 0;

                self.resetSelection();

                self.createReconciliation();

            });

            $("#btnChangeReconciliation").on("click", function () {

                self.prepareNewReconciliation();

            });

            $("#btnRefresh").on("click", function () {

                if (
                    self.state.reconciliationId
                ) {
                    self.reloadBankTable();
                }

            });

            $("#btnMatch").on("click", function () {

                self.matchSelected();

            });

            $("#btnOtherMatch").on("click", function () {

                self.state.candidateMode = 1;

                self.reloadCandidateTable();

            });

            $("#btnRemoveMatch").on("click", function () {

                self.removeMatch();

            });

            $("#btnAutoMatch").on("click", function () {

                self.autoMatch();

            });

            $("#btnFinalize").on("click", function () {

                self.finalizeReconciliation();

            });

            $("#btnGroupMatch").on("click", function () {

                if (
                    !self.state.selectedBankTransactionId
                ) {

                    self.showError(
                        "ابتدا یک تراکنش بانکی را انتخاب کنید."
                    );

                    return;
                }

                self.state.candidateMode = 2;

                self.reloadCandidateTable();

            });

        },

        selectBankTransaction: function (id) {

            var self = this;
            var selectedTransaction = null;

            this.state.selectedBankTransactionId =
                Number(id);

            this.state.selectedAccountingDetailId =
                null;

            this.state.selectedBankTransactionIds =
                [Number(id)];

            this.state.selectedAccountingDetailIds =
                [];

            this.state.candidateMode = 1;

            selectedTransaction =
                this.state.bankTable
                    .row("#" + id)
                    .data();

            if (selectedTransaction) {

                $("#selectedTransactionDate")
                    .text(self.formatDate(selectedTransaction.TransactionDate));

                $("#selectedTransactionDescription")
                    .text(selectedTransaction.Description || "-");

                $("#selectedTransactionAmount")
                    .text(self.formatAmount(selectedTransaction.Amount));

                $("#selectedTransactionRemaining")
                    .text(self.formatAmount(selectedTransaction.RemainingAmount));

            }

            $("#btnMatch")
                .prop("disabled", true);

            $("#btnOtherMatch")
                .prop("disabled", false);

            $("#btnRemoveMatch")
                .prop("disabled", false);

            $("#matchScore")
                .text("-");

            $("#matchScoreLabel")
                .text("میزان تطابق");

            this.reloadCandidateTable();

        },

        selectAccountingDetail: function (candidate) {

            this.state.selectedAccountingDetailId =
                Number(
                    candidate.AccountingDocDetailId
                );

            this.state.selectedAccountingDetailIds =
                [
                    Number(
                        candidate.AccountingDocDetailId
                    )
                ];

            $("#btnMatch")
                .prop(
                    "disabled",
                    !candidate.CanMatch
                );

            $("#matchScore")
                .text(
                    candidate.AmountMatched
                        ? "100%"
                        : "-"
                );

            $("#matchScoreLabel")
                .text(
                    candidate.AmountMatched
                        ? "تطبیق مبلغ"
                        : "میزان تطابق"
                );

        },

        reloadBankTable: function () {

            if (!this.state.bankTable) {
                return;
            }

            this.state.bankTable
                .ajax
                .reload(null, true);

        },

        reloadCandidateTable: function () {

            if (!this.state.candidateTable) {
                return;
            }

            this.state.candidateTable
                .ajax
                .reload(null, true);

            this.state.candidateTable
                .columns
                .adjust();

        },

        createReconciliation: function () {

            var self = this;

            var bankAccountId =
                Number(
                    $("#BankAccountId").val()
                );

            var fromDate =
                $("#FromDate").val();

            var toDate =
                $("#ToDate").val();

            if (!bankAccountId) {

                this.showError(
                    "حساب بانکی را انتخاب کنید."
                );

                return;
            }

            if (!fromDate || !toDate) {

                this.showError(
                    "بازه تاریخ را مشخص کنید."
                );

                return;
            }

            var fromDateValue =
                $("#FromDate")
                    .MdPersianDateTimePicker("getDate");

            var toDateValue =
                $("#ToDate")
                    .MdPersianDateTimePicker("getDate");

            if (!fromDateValue || !toDateValue) {

                this.showError(
                    "تاریخ انتخاب‌شده معتبر نیست."
                );

                return;
            }

            $("#btnLoad")
                .prop("disabled", true);

            $.ajax({

                url:
                    "/BankReconciliation/Create",

                type:
                    "POST",

                headers: {
                    "RequestVerificationToken": $("#forgeryToken").val()
                },

                data:
                    JSON.stringify({

                        BankAccountId:
                            bankAccountId,

                        FromDate:
                            fromDateValue,

                        ToDate:
                            toDateValue

                    }),

                contentType:
                    "application/json; charset=utf-8",

                dataType:
                    "json"

            })
                .done(function (response) {

                    if (!self.handleResponse(response)) {
                        return;
                    }

                    self.state.reconciliationId =
                        response.Result.Id;

                    self.state.reconciliationState =
                        Number(
                            response.Result.State
                        );

                    self.state.page = 1;
                    self.state.status = null;
                    self.state.search = "";

                    $("#txtSearch")
                        .val("");

                    self.showSuccess(
                        response.Message ||
                        "مغایرت بانکی با موفقیت ایجاد شد."
                    );

                    self.resetSelection();
                    self.renderReconciliationContext(response.Result.Reconciliation || response.Result);
                    self.updateButtons();
                    self.reloadBankTable();

                })
                .fail(function () {

                    self.showError(
                        "خطا در ایجاد مغایرت بانکی."
                    );

                })
                .always(function () {

                    $("#btnLoad")
                        .prop("disabled", false);

                });

        },

        matchSelected: function () {

            var self = this;

            if (
                !this.state.selectedBankTransactionId ||
                !this.state.selectedAccountingDetailId
            ) {

                this.showError(
                    "تراکنش بانک و سند مالی را انتخاب کنید."
                );

                return;
            }

            $.ajax({

                url:
                    "/BankReconciliation/Match",

                type:
                    "POST",

                data:
                    JSON.stringify({

                        ReconciliationId:
                            self.state.reconciliationId,

                        BankTransactionId:
                            self.state.selectedBankTransactionId,

                        AccountingDocDetailId:
                            self.state.selectedAccountingDetailId

                    }),

                contentType:
                    "application/json; charset=utf-8",

                dataType:
                    "json"

            })
                .done(function (response) {

                    if (!self.handleResponse(response)) {
                        return;
                    }

                    self.showSuccess(
                        response.Message ||
                        "تطبیق با موفقیت ثبت شد."
                    );

                    self.resetSelection();
                    self.reloadBankTable();
                    self.reloadCandidateTable();

                })
                .fail(function () {

                    self.showError(
                        "خطا در ثبت تطبیق."
                    );

                });

        },

        getMatches: function (callback) {

            var self = this;

            $.ajax({

                url:
                    "/BankReconciliation/GetMatches",

                type:
                    "GET",

                data: {

                    reconciliationId:
                        self.state.reconciliationId,

                    bankTransactionId:
                        self.state.selectedBankTransactionId

                },

                dataType:
                    "json"

            })
                .done(function (response) {

                    if (!self.handleResponse(response)) {
                        return;
                    }

                    callback(
                        response.Result || []
                    );

                })
                .fail(function () {

                    self.showError(
                        "خطا در دریافت تطبیق‌ها."
                    );

                });

        },

        removeMatch: function () {

            var self = this;

            if (
                !this.state.selectedBankTransactionId
            ) {
                return;
            }

            this.getMatches(function (matches) {

                if (!matches.length) {

                    self.showError(
                        "برای این تراکنش تطبیقی ثبت نشده است."
                    );

                    return;
                }

                var match =
                    matches[matches.length - 1];

                $.ajax({

                    url:
                        "/BankReconciliation/RemoveMatch",

                    type:
                        "POST",

                    data: {

                        reconciliationId:
                            self.state.reconciliationId,

                        matchId:
                            match.Id

                    }

                })
                    .done(function (response) {

                        if (!self.handleResponse(response)) {
                            return;
                        }

                        self.showSuccess(
                            response.Message ||
                            "تطبیق حذف شد."
                        );

                        self.reloadBankTable();
                        self.reloadCandidateTable();

                    })
                    .fail(function () {

                        self.showError(
                            "خطا در حذف تطبیق."
                        );

                    });

            });

        },

        autoMatch: function () {

            var self = this;

            if (!this.state.reconciliationId) {
                return;
            }

            if (!confirm(
                "تطبیق خودکار برای این مغایرت اجرا شود؟"
            )) {
                return;
            }

            $.ajax({

                url:
                    "/BankReconciliation/AutoMatch",

                type:
                    "POST",

                data: {

                    reconciliationId:
                        self.state.reconciliationId

                }

            })
                .done(function (response) {

                    if (!self.handleResponse(response)) {
                        return;
                    }

                    var results =
                        response.Result || [];

                    var matched = 0;

                    $.each(
                        results,
                        function (_, item) {

                            if (item.Matched) {
                                matched++;
                            }

                        }
                    );

                    self.showSuccess(
                        matched +
                        " تراکنش به صورت خودکار تطبیق داده شد."
                    );

                    self.reloadBankTable();

                })
                .fail(function () {

                    self.showError(
                        "خطا در اجرای تطبیق خودکار."
                    );

                });

        },

        finalizeReconciliation: function () {

            var self = this;

            if (!this.state.reconciliationId) {
                return;
            }

            if (!confirm(
                "مغایرت بانکی نهایی شود؟"
            )) {
                return;
            }

            $.ajax({

                url:
                    "/BankReconciliation/Finalize",

                type:
                    "POST",

                data: {

                    reconciliationId:
                        self.state.reconciliationId

                }

            })
                .done(function (response) {

                    if (!self.handleResponse(response)) {
                        return;
                    }

                    self.showSuccess(
                        response.Message ||
                        "مغایرت بانکی نهایی شد."
                    );

                    self.reloadBankTable();

                })
                .fail(function () {

                    self.showError(
                        "خطا در نهایی‌سازی مغایرت بانکی."
                    );

                });

        },

        renderSummary: function (summary) {

            if (!summary) {
                return;
            }

            $("#summaryTotal")
                .text(
                    this.formatNumber(
                        summary.TotalTransactions
                    )
                );

            $("#summaryMatched")
                .text(
                    this.formatNumber(
                        summary.MatchedTransactions
                    )
                );

            $("#summaryPartial")
                .text(
                    this.formatNumber(
                        summary.PartialTransactions
                    )
                );

            $("#summaryUnmatched")
                .text(
                    this.formatNumber(
                        summary.UnmatchedTransactions
                    )
                );

            $("#summaryRemaining")
                .text(
                    this.formatAmount(
                        summary.RemainingAmount
                    )
                );

            var percentage =
                this.number(
                    summary.MatchPercentage
                );

            $("#matchPercentage")
                .text(
                    percentage.toFixed(0) + "%"
                );

            $("#matchProgress")
                .css(
                    "width",
                    Math.max(
                        0,
                        Math.min(100, percentage)
                    ) + "%"
                );

            $("#matchProgressText")
                .text(
                    this.formatNumber(
                        summary.MatchedTransactions
                    ) +
                    " از " +
                    this.formatNumber(
                        summary.TotalTransactions
                    ) +
                    " تراکنش تطبیق شده"
                );

            $("#remainingText")
                .text(
                    this.formatNumber(
                        summary.UnmatchedTransactions
                    ) +
                    " مورد باقی‌مانده"
                );

            $("#tabAllCount")
                .text(
                    this.formatNumber(
                        summary.TotalTransactions
                    )
                );

            $("#tabPartialCount")
                .text(
                    this.formatNumber(
                        summary.PartialTransactions
                    )
                );

            $("#tabUnmatchedCount")
                .text(
                    this.formatNumber(
                        summary.UnmatchedTransactions
                    )
                );

            $("#tabMatchedCount")
                .text(
                    this.formatNumber(
                        summary.MatchedTransactions
                    )
                );

        },

        resetSelection: function () {

            this.state.selectedBankTransactionId = null;
            this.state.selectedAccountingDetailId = null;

            this.state.selectedBankTransactionIds = [];
            this.state.selectedAccountingDetailIds = [];

            if (this.state.bankTable) {
                this.state.bankTable
                    .rows()
                    .deselect();
            }

            if (this.state.candidateTable) {
                this.state.candidateTable
                    .rows()
                    .deselect();
            }

            $("#btnMatch")
                .prop("disabled", true);

            $("#btnOtherMatch")
                .prop("disabled", true);

            $("#btnRemoveMatch")
                .prop("disabled", true);

            $("#matchScore")
                .text("-");

            $("#matchScoreLabel")
                .text("میزان تطابق");

            $("#selectedTransactionDate")
                .text("-");

            $("#selectedTransactionDescription")
                .text("تراکنشی انتخاب نشده است");

            $("#selectedTransactionAmount")
                .text("-");

            $("#selectedTransactionRemaining")
                .text("-");

        },

        prepareNewReconciliation: function () {

            this.state.reconciliationId = 0;
            this.state.reconciliationState = 0;
            this.state.page = 1;
            this.state.status = null;
            this.state.search = "";

            $("#BankAccountId")
                .prop("disabled", false);

            $("#FromDate")
                .prop("disabled", false);

            $("#ToDate")
                .prop("disabled", false);

            $("#txtSearch")
                .val("");

            $("#btnLoad")
                .prop("disabled", false)
                .html('<i class="fas fa-search ml-1"></i> نمایش');

            $("#reconciliationContext")
                .addClass("d-none");

            this.resetSelection();
            this.renderSummary({
                TotalTransactions: 0,
                MatchedTransactions: 0,
                PartialTransactions: 0,
                UnmatchedTransactions: 0,
                RemainingAmount: 0,
                MatchPercentage: 0
            });

            this.reloadBankTable();
            this.reloadCandidateTable();
            this.updateButtons();

        },

        renderReconciliationContext: function (reconciliation) {

            if (!reconciliation) {
                return;
            }

            var title =
                reconciliation.BankAccountTitle ||
                "حساب بانکی";

            var fromDate =
                this.formatDate(reconciliation.FromDate);

            var toDate =
                this.formatDate(reconciliation.ToDate);

            var state =
                Number(reconciliation.State) === 1
                    ? "نهایی شده"
                    : "در حال ویرایش";

            $("#reconciliationContextTitle")
                .text(title);

            $("#reconciliationContextDate")
                .text(fromDate + " تا " + toDate);

            $("#reconciliationContextState")
                .text(state);

            $("#reconciliationContext")
                .removeClass("d-none");

            $("#BankAccountId")
                .prop("disabled", true);

            $("#FromDate")
                .prop("disabled", true);

            $("#ToDate")
                .prop("disabled", true);

            $("#btnLoad")
                .prop("disabled", true)
                .html('<i class="fas fa-check ml-1"></i> بارگذاری شد');

        },

        updateButtons: function () {

            var active =
                this.state.reconciliationId > 0;

            var finalized =
                Number(
                    this.state.reconciliationState
                ) === 1;

            $("#btnAutoMatch")
                .prop(
                    "disabled",
                    !active || finalized
                );

            $("#btnGroupMatch")
                .prop(
                    "disabled",
                    !active || finalized
                );

            $("#btnFinalize")
                .prop(
                    "disabled",
                    !active || finalized
                );

        },

        statusBadge: function (status) {

            status = Number(status);

            if (status === 2) {

                return '<span class="br-status br-status-matched">' +
                    '<i class="fas fa-check"></i> تطبیق شده' +
                    "</span>";

            }

            if (status === 1) {

                return '<span class="br-status br-status-partial">' +
                    '<i class="fas fa-exclamation"></i> نیاز به بررسی' +
                    "</span>";

            }

            return '<span class="br-status br-status-unmatched">' +
                '<i class="fas fa-times"></i> عدم تطبیق' +
                "</span>";

        },

        number: function (value) {

            if (
                value === null ||
                value === undefined ||
                value === ""
            ) {
                return 0;
            }

            return Number(value) || 0;

        },

        formatNumber: function (value) {

            return this.number(value)
                .toLocaleString("en-US");

        },

        formatAmount: function (value) {

            return this.number(value)
                .toLocaleString("en-US");

        },

        escapeHtml: function (value) {

            return $("<div>")
                .text(
                    value === null ||
                    value === undefined
                        ? ""
                        : value
                )
                .html();

        },

        formatDate: function (value) {

            if (!value) {
                return "";
            }

            if (
                typeof value === "string" &&
                value.indexOf("/Date(") === 0
            ) {

                var timestamp =
                    parseInt(
                        value
                            .replace("/Date(", "")
                            .replace(")/", ""),
                        10
                    );

                if (!isNaN(timestamp)) {

                    return new Date(timestamp)
                        .toLocaleDateString("fa-IR");

                }

            }

            return value;

        },

        showError: function (message) {

            if (typeof toastr !== "undefined") {
                toastr.error(message);
            }
            else {
                alert(message);
            }

        },

        showSuccess: function (message) {

            if (typeof toastr !== "undefined") {
                toastr.success(message);
            }
            else {
                alert(message);
            }

        },

        isSuccess: function (response) {

            return response &&
                response.ResponseStatus === 1;

        },

        handleResponse: function (response) {

            if (!this.isSuccess(response)) {

                this.showError(
                    response && response.Message
                        ? response.Message
                        : "خطایی رخ داده است."
                );

                return false;
            }

            return true;

        }

    };

    $(function () {

        BankReconciliation.init();

    });

    window.BankReconciliation =
        BankReconciliation;

})(window, jQuery);
