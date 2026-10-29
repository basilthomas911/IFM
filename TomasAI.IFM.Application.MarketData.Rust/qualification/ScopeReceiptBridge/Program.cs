using MessagePack;
using TomasAI.IFM.Application.MarketData.Pricing;
var content=await File.ReadAllBytesAsync(args[0]);
var receipt=MessagePackSerializer.Deserialize<WorkerOptionChainRequest>(content,MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
if(receipt.ScopeId!="trade"||receipt.LeaseId!=new Guid(new byte[]{111,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0})||receipt.GenerationId==Guid.Empty
 ||receipt.ValueDate!=new DateOnly(2026,10,27)||receipt.MaturityDate!=new DateOnly(2026,11,10)||receipt.Options.Length!=1)throw new InvalidDataException("Native scope receipt identity/membership changed.");
var option=receipt.Options[0];
if(option.Strike!=6000m||!option.IsCall||option.Pricing.Contract.ContractId!="ES20261110C6000"||option.Pricing.GenerationId!=receipt.GenerationId
 ||option.Pricing.Contract.InstrumentId!=43||option.Pricing.Calendar.TradingDates.IsDefaultOrEmpty||string.IsNullOrWhiteSpace(option.Pricing.PricerVersion)
 ||receipt.LeaseExpiresAtUtc>option.Pricing.ValidUntilUtc)throw new InvalidDataException("Native scope receipt lost exact reviewed context.");
var restored=MessagePackSerializer.Deserialize<WorkerOptionChainRequest>(MessagePackSerializer.Serialize(receipt));
if(!MessagePackSerializer.Serialize(restored).AsSpan().SequenceEqual(MessagePackSerializer.Serialize(receipt)))throw new InvalidDataException("Native context failed managed roundtrip.");
Console.WriteLine("PASS native scope receipt: exact existing WorkerOptionChainRequest DTO, stable business owner, native generation, exact strike/right, reviewed calendar/rate/context and bounded expiry; no C# context preparation.");
