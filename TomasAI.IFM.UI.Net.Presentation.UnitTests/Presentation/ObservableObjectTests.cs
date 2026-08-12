using FluentAssertions;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Presentation;

public class ObservableObjectTests
{
    [Fact]
    public void SetProperty_RaisesOnlyWhenValueChanges()
    {
        var subject = new TestObservable();
        var changes = new List<string?>();
        subject.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        subject.Name = "first";
        subject.Name = "first";
        subject.Name = "second";

        changes.Should().Equal(nameof(subject.Name), nameof(subject.Name));
    }

    sealed class TestObservable : ObservableObject
    {
        string _name = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
    }
}
