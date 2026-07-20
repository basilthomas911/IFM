using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.PredictiveModel.Client;

public class PredictiveModelQueryServiceRestClientApi(IPredictiveModelQueryServiceRestApiOptions options, IJsonSerializer serializer) 
    : QueryServiceRestApiClient(options, serializer), IPredictiveModelQueryService
{
}
