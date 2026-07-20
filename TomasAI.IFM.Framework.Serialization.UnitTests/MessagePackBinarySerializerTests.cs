using System;
using System.Collections.Generic;
using Xunit;

namespace TomasAI.IFM.Framework.Serialization.UnitTests;

/// <summary>
/// Unit tests for <see cref="MessagePackBinarySerializer"/> covering typed and typeless
/// serialization/deserialization flows, collections, polymorphism, and edge cases.
/// </summary>
public class MessagePackBinarySerializerTests
{
 readonly MessagePackBinarySerializer _sut = new();

 /// <summary>
 /// Verify that Serialize&lt;T&gt;(null) returns null for reference types.
 /// </summary>
 [Fact]
 public void Serialize_Typed_Null_ReturnsNull()
 {
 // arrange
 SimplePoco? input = null;

 // act
 var bytes = _sut.Serialize(input);

 // assert
 Assert.Null(bytes);
 }

 /// <summary>
 /// Verify that Serialize(object) returns null for a null instance.
 /// </summary>
 [Fact]
 public void Serialize_Object_Null_ReturnsNull()
 {
 // arrange
 object? input = null;

 // act
 var bytes = _sut.Serialize(input!);

 // assert
 Assert.Null(bytes);
 }

 /// <summary>
 /// Verify that Deserialize&lt;T&gt;(null) returns default(T) for value and reference types.
 /// </summary>
 [Fact]
 public void Deserialize_Typed_Null_ReturnsDefault()
 {
 // arrange
 byte[]? bytes = null;

 // act
 var obj = _sut.Deserialize<SimplePoco>(bytes!);
 var number = _sut.Deserialize<int>(bytes!);

 // assert
 Assert.Null(obj);
 Assert.Equal(0, number);
 }

 /// <summary>
 /// Verify that Deserialize&lt;T&gt; with an empty payload returns default(T).
 /// </summary>
 [Fact]
 public void Deserialize_Typed_Empty_ReturnsDefault()
 {
 // arrange
 var bytes = Array.Empty<byte>();

 // act
 var obj = _sut.Deserialize<SimplePoco>(bytes);
 var number = _sut.Deserialize<int>(bytes);

 // assert
 Assert.Null(obj);
 Assert.Equal(0, number);
 }

 /// <summary>
 /// Verify that Deserialize(object) returns null for null and empty payloads.
 /// </summary>
 [Fact]
 public void Deserialize_Object_NullOrEmpty_ReturnsNull()
 {
 // arrange
 byte[]? nullBytes = null;
 var emptyBytes = Array.Empty<byte>();

 // act
 var obj1 = _sut.Deserialize<byte[]>(nullBytes!);
 var obj2 = _sut.Deserialize<byte[]>(emptyBytes);

 // assert
 Assert.Null(obj1);
 Assert.Null(obj2);
 }

 /// <summary>
 /// Round-trip a primitive value using typed serialization and ensure equality.
 /// </summary>
 [Fact]
 public void Roundtrip_Typed_Primitive_Ok()
 {
 // arrange
 var input =123456789;

 // act
 var bytes = _sut.Serialize(input);
 var result = _sut.Deserialize<int>(bytes!);

 // assert
 Assert.Equal(input, result);
 }

 /// <summary>
 /// Round-trip a POCO using typed serialization (contractless resolver).
 /// </summary>
 [Fact]
 public void Roundtrip_Typed_Poco_Contractless_Ok()
 {
     // arrange
     var input = new SimplePoco
     {
     Id =42,
     Name = "Alpha",
     When = new DateTime(2024,12,31,23,59,59, DateTimeKind.Utc),
     Values = new[] {1.1,2.2,3.3 },
     Child = new ChildPoco { Code = "C-001" }
     };

     // act
     var bytes = _sut.Serialize(input);
     var result = _sut.Deserialize<SimplePoco>(bytes!);

     // assert
     Assert.NotNull(result);
     Assert.Equal(input.Id, result!.Id);
     Assert.Equal(input.Name, result.Name);
     Assert.Equal(input.When, result.When);
     Assert.Equal(input.Values, result.Values);
     Assert.NotNull(result.Child);
     Assert.Equal(input.Child!.Code, result.Child!.Code);
 }

 // <summary>
 /// Round-trip a POCO using typed serialization (contractless resolver).
 /// </summary>
 [Fact]
 public void Roundtrip_Typed_Generic_Contractless_Ok()
 {
     // arrange
     var input = SampleData.TestCommand;
        
     // act
     var bytes = _sut.Serialize(input);
     var result = _sut.Deserialize<TestCommand>(bytes!);

      // assert
      Assert.NotNull(result);
      Assert.Equal(input.MsgString, result!.MsgString);
 }

 /// <summary>
 /// Round-trip a list of POCOs using typed serialization to verify collection support.
 /// </summary>
 [Fact]
 public void Roundtrip_Typed_Collection_Ok()
 {
 // arrange
 var input = new List<SimplePoco>
 {
 new() { Id =1, Name = "One", When = new DateTime(2025,1,1,0,0,0, DateTimeKind.Utc), Values = new[] {1.0 }, Child = new ChildPoco { Code = "A" } },
 new() { Id =2, Name = "Two", When = new DateTime(2025,1,2,0,0,0, DateTimeKind.Utc), Values = new[] {2.0,3.0 }, Child = new ChildPoco { Code = "B" } }
 };

 // act
 var bytes = _sut.Serialize(input);
 var result = _sut.Deserialize<List<SimplePoco>>(bytes!);

 // assert
 Assert.NotNull(result);
 Assert.Equal(input.Count, result!.Count);
 for (var i =0; i < input.Count; i++)
 {
 Assert.Equal(input[i].Id, result[i].Id);
 Assert.Equal(input[i].Name, result[i].Name);
 Assert.Equal(input[i].When, result[i].When);
 Assert.Equal(input[i].Values, result[i].Values);
 Assert.Equal(input[i].Child!.Code, result[i].Child!.Code);
 }
 }

 /// <summary>
 /// Round-trip a dictionary using typed serialization to verify key/value mapping.
 /// </summary>
 [Fact]
 public void Roundtrip_Typed_Dictionary_Ok()
 {
 // arrange
 var input = new Dictionary<string, SimplePoco>
 {
 ["x"] = new() { Id =10, Name = "Ten", When = DateTime.SpecifyKind(new DateTime(2025,5,5,5,5,5), DateTimeKind.Utc), Values = new[] {9.9 }, Child = new ChildPoco { Code = "X" } },
 ["y"] = new() { Id =20, Name = "Twenty", When = DateTime.SpecifyKind(new DateTime(2025,6,6,6,6,6), DateTimeKind.Utc), Values = new[] {8.8 }, Child = new ChildPoco { Code = "Y" } }
 };

 // act
 var bytes = _sut.Serialize(input);
 var result = _sut.Deserialize<Dictionary<string, SimplePoco>>(bytes!);

 // assert
 Assert.NotNull(result);
 Assert.Equal(input.Count, result!.Count);
 foreach (var kvp in input)
 {
 Assert.True(result.ContainsKey(kvp.Key));
 var expected = kvp.Value;
 var actual = result[kvp.Key];
 Assert.Equal(expected.Id, actual.Id);
 Assert.Equal(expected.Name, actual.Name);
 Assert.Equal(expected.When, actual.When);
 Assert.Equal(expected.Values, actual.Values);
 Assert.Equal(expected.Child!.Code, actual.Child!.Code);
 }
 }

 /// <summary>
 /// Verify that deserializing invalid/corrupt payload using the typed API throws an exception.
 /// </summary>
 [Fact]
 public void Deserialize_Typed_InvalidBytes_Throws()
 {
 // arrange
 var bytes = GetRandomBytes(32);

 // act
 var act = new Action(() => _sut.Deserialize<SimplePoco>(bytes));

 // assert
 Assert.ThrowsAny<Exception>(act);
 }

 private static byte[] GetRandomBytes(int length)
 {
 var rnd = new Random(1234);
 var bytes = new byte[length];
 rnd.NextBytes(bytes);
 return bytes;
 }

 public class SimplePoco
 {
 public int Id { get; set; }
 public string? Name { get; set; }
 public DateTime When { get; set; }
 public double[]? Values { get; set; }
 public ChildPoco? Child { get; set; }
 }

 public class ChildPoco
 {
 public string? Code { get; set; }
 }

 public class Animal
 {
 public string? Name { get; set; }
 }

 public sealed class Dog : Animal
 {
 public string? Breed { get; set; }
 }
}