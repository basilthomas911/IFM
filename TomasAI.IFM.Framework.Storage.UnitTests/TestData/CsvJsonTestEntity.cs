using System;

namespace TomasAI.IFM.Framework.Storage.UnitTests.TestData;

public class CsvJsonTestEntity
{
    public string Name { get; set; }
    public int Age { get; set; }
    public bool IsActive { get; set; }
    public double Score { get; set; }
    public decimal Balance { get; set; }
    public float Rating { get; set; }
    public long BigNumber { get; set; }
    public short SmallNumber { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid UniqueId { get; set; }
}
