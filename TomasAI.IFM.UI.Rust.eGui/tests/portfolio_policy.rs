//! Backend commands are exercised by backend_nats; these cases protect input
//! semantics that must be decided before any command can be emitted.
use ifm_ui_egui::backend::schema::Registry;
#[test]
fn policy_contract_separates_version_from_aggregate_revision() {
    let registry = Registry::compiled().unwrap();
    let shape = registry.describe("TomasAI.IFM.Domain.Portfolio.Shared.Commands.ActivateAndAssignPortfolioFinancialPolicyCommand").unwrap();
    assert!(
        shape
            .members
            .iter()
            .any(|(name, index, _)| name == "PolicyVersion" && *index == 6)
    );
    assert!(
        shape
            .members
            .iter()
            .any(|(name, index, _)| name == "ExpectedPolicyRevision" && *index == 7)
    );
    assert!(
        shape
            .members
            .iter()
            .any(|(name, index, _)| name == "ExpectedPortfolioRevision" && *index == 8)
    );
}
