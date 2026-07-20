using QLNet;
using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.StatusConsole.ViewModels;

public class DenormalizerLogReadModel
{
    private readonly string _commandId;
    private readonly DateTime _denormalizerDate;
    private readonly string _denormalizerName;
    private readonly string _denormalizerData;
    private readonly string _userName;
    private readonly string _errorMessage;
    private readonly int _errorCode;
    private readonly ErrorType _errorType;
    private readonly string _errorData;

    public DenormalizerLogReadModel(
        string commandId,
        DateTime denormalizerDate,
        string denormalizerName,
        string denormalizerData,
        string userName,
        string errorMessage,
        int errorCode,
        ErrorType errorType,
        string errorData)
    {
        _commandId = commandId;
        _denormalizerDate = denormalizerDate;
        _denormalizerName = denormalizerName;
        _denormalizerData = denormalizerData;
        _userName = userName;
        _errorMessage = errorMessage;
        _errorCode = errorCode;
        _errorType = errorType;
        _errorData = errorData;
    }

    public string CommandId => _commandId;
    public DateTime DenormalizerDate => _denormalizerDate;
    public string DenormalizerName => _denormalizerName;
    public string DenormalizerData => _denormalizerData;
    public string UserName => _userName;
    public string ErrorMessage => _errorMessage;
    public int ErrorCode => _errorCode;
    public ErrorType ErrorType => _errorType;
    public string ErrorData => _errorData;

}
