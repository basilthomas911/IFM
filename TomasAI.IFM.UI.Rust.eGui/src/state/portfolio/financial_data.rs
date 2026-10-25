//! Scoped ledger panels. Each read retains its own revision/cut; transaction
//! history is never used to invent a cash balance or spending permission.
use super::*;
use crate::services::{
    business_context::BusinessContext,
    financial::{self, AccessScope, Read},
};
pub(super) struct LedgerData {
    pub portfolio: i32,
    pub fund: i32,
    pub balances: Option<Read>,
    pub transactions: Option<Read>,
    pub reservations: Option<Read>,
    pub journal: Option<Read>,
    pub message: String,
}
fn financial_kind(name: &str) -> String {
    format!("{}.{name}", financial::ROOT)
}
impl LedgerData {
    fn empty(portfolio: i32, fund: i32) -> Self {
        Self {
            portfolio,
            fund,
            balances: None,
            transactions: None,
            reservations: None,
            journal: None,
            message: String::new(),
        }
    }
    pub async fn load(
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        portfolio: i32,
        fund: i32,
        deadline: tokio::time::Instant,
    ) -> Self {
        let mut state = Self::empty(portfolio, fund);
        let result=async{
            let b=registry.encode(&financial_kind("GetAccountBalancesRequest"),&BTreeMap::new())?;
            let t=registry.encode(&financial_kind("GetFundTransactionsPageRequest"),&BTreeMap::from([("PageSize".into(),100.into())]))?;
            let r=registry.encode(&financial_kind("GetFundReservationsPageRequest"),&BTreeMap::from([("PageSize".into(),100.into())]))?;
            let query=|name,parameters|financial::query(backend,registry,context,name,portfolio,Some(fund),parameters,AccessScope::LedgerEditor,deadline);
            let (balances,transactions,reservations)=tokio::try_join!(query("GetAccountBalancesQuery",b),query("GetFundTransactionsPageQuery",t),query("GetFundReservationsPageQuery",r))?;
            if [balances.status,transactions.status,reservations.status].contains(&3){return Err("Financial authority is unavailable".into());}
            state.validate_page(registry,&transactions,"FinancialTransactionRow","Transactions")?;state.validate_page(registry,&reservations,"FinancialReservationView","Reservations")?;
            state.message=if balances.status==2{"No financial book is configured for this Fund.".into()}else{
                let row=registry.project(&financial_kind("FinancialBalanceSnapshot"),balances.found()?)?;
                if row["MigrationQualified"]==false{"Financial book is unqualified; new spending is disabled.".into()}else{format!("Financial authority: {}. Balances revision {}; transactions cut {}; reservations cut {}.",text(&row["OperatingState"]),balances.financial_revision,page_cut(&transactions),page_cut(&reservations))}
            };
            state.balances=Some(balances);state.transactions=Some(transactions);state.reservations=Some(reservations);
            if let Some(id)=state.transaction_rows()?.first().and_then(|row|registry.project(&financial_kind("FinancialTransactionRow"),row).ok()).and_then(|row|row["JournalId"].as_i64()).filter(|id|*id>0){
                state.load_journal(backend,registry,context,id,deadline).await?;
            }Ok::<_,String>(())
        }.await;
        if let Err(error) = result {
            state.message = format!("Financial data unavailable: {error}");
        }
        state
    }
    fn validate_page(
        &self,
        registry: &Registry,
        read: &Read,
        item: &str,
        kind: &str,
    ) -> Result<(), String> {
        if read.status == 2 {
            return Ok(());
        }
        let row = registry.project(
            &financial_kind(&format!("FinancialPage`1[{}]", financial_kind(item))),
            read.found()?,
        )?;
        if row["Items"].as_array().is_none_or(|rows| rows.len() > 100) {
            return Err("Financial page exceeds its requested bound".into());
        }
        let cut = row["AsOfRevision"]
            .as_i64()
            .filter(|v| *v >= 0)
            .ok_or("Invalid financial page cut")?;
        let cursor = &row["NextCursor"];
        if !cursor.is_null()
            && (cursor["PortfolioId"] != self.portfolio
                || cursor["FundId"] != self.fund
                || cursor["AsOfRevision"] != cut
                || cursor["Kind"] != kind)
        {
            return Err("Financial cursor crossed the selected Fund or revision cut".into());
        }
        Ok(())
    }
    pub fn transaction_rows(&self) -> Result<Vec<Value>, String> {
        page_rows(self.transactions.as_ref())
    }
    pub fn reservation_rows(&self) -> Result<Vec<Value>, String> {
        page_rows(self.reservations.as_ref())
    }
    pub async fn next(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        transactions: bool,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let old = if transactions {
            self.transactions.as_ref()
        } else {
            self.reservations.as_ref()
        }
        .ok_or("Load financial data first")?;
        let cursor = old
            .value
            .as_ref()
            .and_then(Value::as_array)
            .and_then(|v| v.get(1))
            .filter(|v| !v.is_nil())
            .ok_or("No next financial page")?
            .clone();
        let request = if transactions {
            "GetFundTransactionsPage"
        } else {
            "GetFundReservationsPage"
        };
        let parameters = registry.encode(
            &financial_kind(&format!("{request}Request")),
            &BTreeMap::from([
                ("PageSize".into(), 100.into()),
                ("Cursor".into(), cursor.clone()),
            ]),
        )?;
        let read = financial::query(
            backend,
            registry,
            context,
            &format!("{request}Query"),
            self.portfolio,
            Some(self.fund),
            parameters,
            AccessScope::LedgerEditor,
            deadline,
        )
        .await?;
        if read.status != 1 {
            return Err("Financial page is unavailable".into());
        }
        self.validate_page(
            registry,
            &read,
            if transactions {
                "FinancialTransactionRow"
            } else {
                "FinancialReservationView"
            },
            if transactions {
                "Transactions"
            } else {
                "Reservations"
            },
        )?;
        if page_cut(&read) != page_cut(old) {
            return Err("Financial paging changed its revision cut".into());
        }
        if read
            .value
            .as_ref()
            .and_then(Value::as_array)
            .and_then(|v| v.get(1))
            == Some(&cursor)
        {
            return Err("Financial paging did not advance".into());
        }
        if transactions {
            self.transactions = Some(read);
            self.journal = None;
        } else {
            self.reservations = Some(read);
        }
        Ok(())
    }
    pub async fn load_journal(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        id: i64,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        if id <= 0 {
            return Err("Select a valid journal".into());
        }
        self.journal = None;
        let request = registry.encode(
            &financial_kind("GetJournalRequest"),
            &BTreeMap::from([("JournalId".into(), id.into())]),
        )?;
        let read = financial::query(
            backend,
            registry,
            context,
            "GetJournalQuery",
            self.portfolio,
            Some(self.fund),
            request,
            AccessScope::LedgerEditor,
            deadline,
        )
        .await?;
        if read.status == 3 {
            return Err("Financial journal unavailable".into());
        }
        self.journal = Some(read);
        Ok(())
    }
}
fn page_cut(read: &Read) -> i64 {
    read.value
        .as_ref()
        .and_then(Value::as_array)
        .and_then(|v| v.get(2))
        .and_then(Value::as_i64)
        .unwrap_or_default()
}
fn page_rows(read: Option<&Read>) -> Result<Vec<Value>, String> {
    let Some(read) = read else {
        return Ok(vec![]);
    };
    if read.status == 2 {
        return Ok(vec![]);
    }
    read.found()?
        .as_array()
        .and_then(|v| v.first())
        .and_then(Value::as_array)
        .filter(|v| v.len() <= 100)
        .cloned()
        .ok_or("Invalid financial page rows".into())
}
