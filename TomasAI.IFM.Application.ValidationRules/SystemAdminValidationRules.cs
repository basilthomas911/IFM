using System;
using System.Collections.Generic;
using System.Text;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.SystemAdmin.ViewModels;

namespace TomasAI.IFM.Application.ValidationRules
{
    public class SystemAdminValidationRules : IDataValidation
    {

        public ValidationError[] Execute(FuturesContractViewModel futuresContract) => ((IDataValidation)this).Validate<FuturesContractViewModel>(futuresContract, new FuturesContractValidator());

        private class SystemAdminValidationRules : AbstractValidator<FuturesContractViewModel>
        {
            public FuturesContractValidator()
            {
                RuleFor(x => x.SecurityType).NotEmpty().Equal("FUT").WithMessage("SecurityType must be FUT");
                RuleFor(x => x.ContractId).NotEmpty().WithMessage("ContractId is required");
                RuleFor(x => x.Description).NotEmpty().WithMessage("Description is required");
                RuleFor(x => x.Symbol).NotEmpty().WithMessage("Symbol is required");
                RuleFor(x => x.LocalSymbol).NotEmpty().WithMessage("LocalSymbol is required");
                RuleFor(x => x.Currency).NotEmpty().WithMessage("Currency is required");
                RuleFor(x => x.Exchange).NotEmpty().WithMessage("Exchange is required");
                RuleFor(x => x.Multiplier).NotEmpty().WithMessage("Multiplier is required");
            }
        }
    }
}
