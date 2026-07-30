using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

public interface IOptionPricerDbContext : IObjectRepository<OptionPricerDbContext>, IOptionPricerDbReadContext, IOptionPricerDbWriteContext
{
    IOptionPricerDbReadContext DbReader { get; }
    IOptionPricerDbWriteContext DbWriter { get; }
}
