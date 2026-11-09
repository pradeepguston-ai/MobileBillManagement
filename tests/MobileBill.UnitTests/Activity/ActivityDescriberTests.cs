using MobileBill.Api.Activity;
using MobileBill.Application.Activity;
using MobileBill.Application.Billing;
using MobileBill.Application.MasterData;
using MobileBill.Application.Reports;
using MobileBill.Domain.Enums;

namespace MobileBill.UnitTests.Activity;

public sealed class ActivityDescriberTests
{
    private static readonly Guid Id = Guid.NewGuid();

    [Theory]
    [InlineData("BillBatches", "Upload", "POST", "Billing", "Uploaded the October 2026 bill PDF")]
    [InlineData("BillBatches", "Validate", "POST", "Billing", "Validated the October 2026 bill batch")]
    [InlineData("BillMatching", "Match", "POST", "Billing", "Matched the October 2026 bill numbers to employees")]
    [InlineData("BillBatchWorkflow", "Submit", "POST", "Approvals", "Submitted the October 2026 bill batch for IT review")]
    [InlineData("BillBatchWorkflow", "Lock", "POST", "Approvals", "Locked the October 2026 billing period")]
    [InlineData("Users", "Deactivate", "POST", "Users", "Deactivated the account of October 2026")]
    [InlineData("Employees", "Resign", "POST", "Employees", "Resigned employee October 2026")]
    public void Names_the_action_and_its_target(string controller, string action, string method, string area, string expected)
    {
        var plan = Plan(controller, action, method, route: Route(Id));

        Assert.NotNull(plan);
        Assert.Equal(area, plan!.Area);
        Assert.Equal(Id, plan.TargetId);
        Assert.Equal(expected, plan.Describe("October 2026"));
    }

    [Theory]
    [InlineData(ApprovalAction.Approve, BillBatchStatus.HRApproval, "Approved the October 2026 bill batch (IT approval)")]
    [InlineData(ApprovalAction.Approve, BillBatchStatus.FinanceApproval, "Approved the October 2026 bill batch (HR approval)")]
    [InlineData(ApprovalAction.Approve, BillBatchStatus.Completed, "Approved the October 2026 bill batch (Finance approval)")]
    [InlineData(ApprovalAction.Reject, BillBatchStatus.Validated, "Rejected the October 2026 bill batch")]
    [InlineData(ApprovalAction.ReturnForCorrection, BillBatchStatus.Validated, "Returned the October 2026 bill batch for correction")]
    public void An_approval_decision_names_the_step_it_approved(ApprovalAction action, BillBatchStatus newStatus, string expected)
    {
        var plan = Plan("BillBatchWorkflow", "Decide", "POST", route: new Dictionary<string, object?> { ["batchId"] = Id.ToString() },
            arguments: new Dictionary<string, object?> { ["request"] = new WorkflowDecisionRequest(action, "comment") }, result: new BillWorkflowResult(Id, newStatus));

        Assert.Equal(expected, plan!.Describe("October 2026"));
    }

    [Fact]
    public void A_master_record_is_named_from_the_saved_record()
    {
        var plan = Plan("Factories", "Create", "POST", result: new ReferenceDataDto(Id, "CAPL", "Concord Apparel", true));

        Assert.Equal(("Masters", "Created factory Concord Apparel (CAPL)"), (plan!.Area, plan.Describe(null)));
    }

    [Fact]
    public void A_deactivated_master_record_is_looked_up_by_its_id()
    {
        var plan = Plan("Factories", "Deactivate", "POST", route: Route(Id));

        Assert.Equal((ActivityTarget.Factory, Id), (plan!.Target, plan.TargetId));
        Assert.Equal("Deactivated factory Concord Apparel (CAPL)", plan.Describe("Concord Apparel (CAPL)"));
    }

    [Fact]
    public void A_bulk_assessment_counts_the_numbers_and_names_the_responsibility()
    {
        var plan = Plan("MonthlyBills", "BulkAssess", "POST",
            arguments: new Dictionary<string, object?> { ["request"] = new { Responsibility = (Responsibility?)Responsibility.ByCompany } },
            result: new BulkAssessmentResultDto(25, 0, []));

        Assert.Equal("Assessed 25 numbers in bulk as By Company", plan!.Describe(null));
    }

    [Fact]
    public void Checking_an_import_file_is_not_recorded_but_saving_it_is()
    {
        Assert.Null(Plan("Employees", "Import", "POST", result: new MasterDataImportResult(10, 3, 2, 5, [], false)));
        Assert.Equal("Imported employee records from Excel (3 new, 2 updated)", Plan("Employees", "Import", "POST", result: new MasterDataImportResult(10, 3, 2, 5, [], true))!.Describe(null));
    }

    [Fact]
    public void Report_emails_count_the_rules_they_were_sent_to()
    {
        var sent = new List<ReportEmailDeliveryDto> { Delivery(), Delivery() };

        Assert.Equal("Emailed the October 2026 report to 2 distribution rules", Plan("ReportDistribution", "Send", "POST", route: new Dictionary<string, object?> { ["batchId"] = Id.ToString() }, result: sent)!.Describe("October 2026"));
    }

    [Fact]
    public void Downloads_are_recorded_by_file_name_but_blank_templates_are_not()
    {
        var plan = ActivityDescriber.Plan("Reports", "ExportExcel", "GET", Route(Id), new Dictionary<string, object?>(), null, "Mobile_Bill_Report_2026_10.xlsx");

        Assert.Equal(("Reports", "Downloaded Mobile_Bill_Report_2026_10.xlsx"), (plan!.Area, plan.Describe(null)));
        Assert.Null(ActivityDescriber.Plan("Employees", "ImportTemplate", "GET", Route(Id), new Dictionary<string, object?>(), null, "Employees.xlsx"));
    }

    [Theory]
    [InlineData("Factories", "Get", "GET")]
    [InlineData("Auth", "Register", "POST")]
    [InlineData("Auth", "ForgotPassword", "POST")]
    [InlineData("Unknown", "Create", "POST")]
    public void Page_views_and_actions_before_signing_in_are_not_recorded(string controller, string action, string method) =>
        Assert.Null(Plan(controller, action, method));

    [Fact]
    public void Sign_ins_and_assistant_questions_are_recorded_without_any_private_content()
    {
        Assert.Equal("Signed in", Plan("Auth", "Login", "POST", arguments: new Dictionary<string, object?> { ["request"] = new { Email = "a@b.lk", Password = "secret-password" } })!.Describe(null));
        var assistant = Plan("Assistant", "Ask", "POST", arguments: new Dictionary<string, object?> { ["request"] = new { Question = "What is Kamal's salary?" } })!.Describe(null);
        Assert.Equal("Asked the AI assistant a question", assistant);
    }

    private static ActivityPlan? Plan(string controller, string action, string method, Dictionary<string, object?>? route = null, Dictionary<string, object?>? arguments = null, object? result = null) =>
        ActivityDescriber.Plan(controller, action, method, route ?? [], arguments ?? [], result, null);

    private static Dictionary<string, object?> Route(Guid id) => new() { ["id"] = id.ToString() };
    private static ReportEmailDeliveryDto Delivery() => new(Guid.NewGuid(), null, "Rule", "a@concord.lk", ReportEmailStatus.Pending, 0, DateTimeOffset.UnixEpoch, "IT", null, null);
}
