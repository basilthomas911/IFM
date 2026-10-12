using Npgsql;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct PortfolioJson(string Value);
