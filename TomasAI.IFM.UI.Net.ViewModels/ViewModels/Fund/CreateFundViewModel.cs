
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.ViewModels.Fund;
public class CreateFundReadModel
{
    private readonly IAppRoot _appRoot;
    public IAppRoot AppRoot => _appRoot;

    public CreateFundReadModel(IAppRoot appRoot)
    {
        _appRoot = appRoot;
    }

    public void LoadNewFundId(Action<int> newFundIdAction, Action<string> onError)
        => _appRoot.GetModel<ReferenceQueryModel>().Execute(async model => {
            model.OnError((_, errorMsg) => onError(errorMsg));
            await model.NewFundIdAsync(newFundId => newFundIdAction(newFundId));
        });

    public void CreateNewFund(FundReadModel newFund, Action onCompleted, Action<string> onError)
        => _appRoot.GetModel<FundCommandModel>().Execute(async model => {
            model.OnError((_, errorMsg) => onError(errorMsg));
            await model.CreateFundAsync(newFund, onCompleted);
        });
}
