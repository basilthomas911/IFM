using System;
using System.Collections.Generic;
using System.Text;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Application.ValidationRules
{
    public class FuturesOptionContractValidationRules : BaseValidationRules
    {

        public ValidationError[] Execute(FuturesOptionContractReadModel futuresOptionContract) => Validate( futuresOptionContract, new FuturesOptionContractValidator() );

        private class FuturesOptionContractValidator : AbstractValidator<FuturesOptionContractReadModel>
        {
            public FuturesOptionContractValidator()
            {
                RuleFor(x => x.SecurityType).NotEmpty().Equal("FUT").WithMessage("SecurityType must be FUT");
                RuleFor(x => x.ContractId).NotEmpty().WithMessage("ContractId is required");
                RuleFor(x => x.Description).NotEmpty().WithMessage("Description is required");
                RuleFor(x => x.Symbol).NotEmpty().WithMessage("Symbol is required");
                RuleFor(x => x.LocalSymbol).NotEmpty().WithMessage("LocalSymbol is required");
                RuleFor(x => x.Currency).NotEmpty().WithMessage("Currency is required");
                RuleFor(x => x.Exchange).NotEmpty().WithMessage("Exchange is required");
                RuleFor(x => x.Multiplier).NotEmpty().WithMessage("Multiplier is required");
                RuleFor(x => x.ContractMonth).NotEmpty().WithMessage("ContractMonth is required");
            }
        }
    }
}
