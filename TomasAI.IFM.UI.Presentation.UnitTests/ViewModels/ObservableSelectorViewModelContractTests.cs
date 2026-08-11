using FluentAssertions;
using System.ComponentModel;
using System.Reflection;
using TomasAI.IFM.UI.Net.ViewModels.Reference;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;
using TomasAI.IFM.UI.Net.ViewModels.Fund;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;

namespace TomasAI.IFM.UI.Presentation.UnitTests.ViewModels;

public class ObservableSelectorViewModelContractTests
{
    public static TheoryData<Type> MigratedViewModels => new()
    {
        typeof(ReferenceViewModel),
        typeof(SystemAdminViewModel),
        typeof(FundTransactionEditorViewModel),
        typeof(CreateFundReadModel),
        typeof(MarketDataViewModel)
    };

    [Theory]
    [MemberData(nameof(MigratedViewModels))]
    public void MigratedViewModel_UsesObservableStateWithoutPublicDelegateCallbacks(Type viewModelType)
    {
        typeof(INotifyPropertyChanged).IsAssignableFrom(viewModelType).Should().BeTrue();

        var delegateMembers = viewModelType
            .GetMembers(BindingFlags.Instance | BindingFlags.Public)
            .Where(member => member switch
            {
                FieldInfo field => typeof(Delegate).IsAssignableFrom(field.FieldType),
                PropertyInfo property => typeof(Delegate).IsAssignableFrom(property.PropertyType),
                _ => false
            });

        delegateMembers.Should().BeEmpty(
            "migrated ViewModels publish state and asynchronous operations rather than view callbacks");
    }
}
