using FormAutomation.Locating;
using OpenQA.Selenium;

namespace FormAutomation.Tests;

public class FieldLocatorTests
{
    private IWebDriver _driver = null!;

    [OneTimeSetUp]
    public void StartDriver() => _driver = DriverFactory.Create(AppSettings.Load([]) with { Headless = true });

    [OneTimeTearDown]
    public void StopDriver() => _driver.Dispose();

    private void Load(string body)
        => _driver.Navigate().GoToUrl("data:text/html;charset=utf-8," +
            Uri.EscapeDataString($"<!doctype html><html><body>{body}</body></html>"));

    private static string Tag(IWebElement element) => element.GetDomAttribute("data-t") ?? "";

    [Test]
    public void LabelFor_Association()
    {
        Load("""
            <label for="a1">First Name</label><label for="a2">Last Name</label>
            <input id="a2" data-t="other"><input id="a1" data-t="target">
            """);
        Assert.That(Tag(FieldLocator.FindTextBox(_driver, "First Name")), Is.EqualTo("target"));
    }

    [Test]
    public void LabelFor_ContradictedByLayout_IsIgnored()
    {
        Load("""
            <div><label for="fname">First Name</label><input id="lname" data-t="target"></div>
            <div><label for="lname">Last Name</label><input id="fname" data-t="other"></div>
            """);
        Assert.That(Tag(FieldLocator.FindTextBox(_driver, "First Name")), Is.EqualTo("target"));
    }

    [Test]
    public void LabelFor_InFlatContainer_StillDecides()
    {
        Load("""
            <div>
              <label for="a">First Name</label><label for="b">Last Name</label>
              <input id="b" data-t="other"><input id="a" data-t="target">
            </div>
            """);
        Assert.That(Tag(FieldLocator.FindTextBox(_driver, "First Name")), Is.EqualTo("target"));
        Assert.That(Tag(FieldLocator.FindTextBox(_driver, "Last Name")), Is.EqualTo("other"));
    }

    [Test]
    public void WrappingLabel_Association()
    {
        Load("""
            <label>Last Name <input data-t="other"></label>
            <label>First Name <input data-t="target"></label>
            """);
        Assert.That(Tag(FieldLocator.FindTextBox(_driver, "First Name")), Is.EqualTo("target"));
    }

    [Test]
    public void UnlinkedLabel_FoundByProximity()
    {
        Load("""
            <div><label>Last Name</label><input data-t="other"></div>
            <div><label>First Name</label><input data-t="target"></div>
            """);
        Assert.That(Tag(FieldLocator.FindTextBox(_driver, "First Name")), Is.EqualTo("target"));
    }

    [Test]
    public void TableLayout_LabelCellThenInputCell()
    {
        Load("""
            <table>
              <tr><td>Last Name</td><td><input data-t="other"></td></tr>
              <tr><td>First Name</td><td><input data-t="target"></td></tr>
            </table>
            """);
        Assert.That(Tag(FieldLocator.FindTextBox(_driver, "First Name")), Is.EqualTo("target"));
    }

    [Test]
    public void AriaLabel_WithoutVisibleLabel()
    {
        Load("""<input aria-label="Last Name" data-t="other"><input aria-label="First Name" data-t="target">""");
        Assert.That(Tag(FieldLocator.FindTextBox(_driver, "First Name")), Is.EqualTo("target"));
    }

    [TestCase("first name *")]
    [TestCase("FIRST NAME:")]
    [TestCase("  First    Name  ")]
    [TestCase("First Name")]
    public void LabelTextVariations(string visibleText)
    {
        Load($"""
            <div><label>Last Name</label><input data-t="other"></div>
            <div><label>{visibleText}</label><input data-t="target"></div>
            """);
        Assert.That(Tag(FieldLocator.FindTextBox(_driver, "First Name")), Is.EqualTo("target"));
    }

    [Test]
    public void LastNameNextToFirstName_FirstNameChosen()
    {
        Load("""
            <div>
              <div><label>First Name</label><input data-t="target"></div>
              <div><label>Last Name</label><input data-t="other"></div>
              <div><label>Father's First Name</label><input data-t="other2"></div>
            </div>
            """);
        Assert.That(Tag(FieldLocator.FindTextBox(_driver, "First Name")), Is.EqualTo("target"));
    }

    [Test]
    public void HiddenDecoy_IsIgnored()
    {
        Load("""
            <div style="display:none"><label>First Name</label><input data-t="decoy"></div>
            <div><label>First Name</label><input data-t="target"></div>
            """);
        Assert.That(Tag(FieldLocator.FindTextBox(_driver, "First Name")), Is.EqualTo("target"));
    }

    [Test]
    public void TwoVisibleFields_ThrowsAndNamesBoth()
    {
        Load("""
            <div><label>First Name</label><input id="a" name="x"></div>
            <div><label>First Name</label><input id="b" name="y"></div>
            """);
        var ex = Assert.Throws<FieldResolutionException>(() => FieldLocator.FindTextBox(_driver, "First Name"))!;
        Assert.That(ex.Message, Does.Contain("Ambiguous").And.Contain("First Name")
            .And.Contain("id=a").And.Contain("id=b").And.Contain("score=80"));
    }

    [Test]
    public void NoMatchingLabel_Throws()
    {
        Load("""<div><label>Email</label><input></div>""");
        var ex = Assert.Throws<FieldResolutionException>(() => FieldLocator.FindTextBox(_driver, "First Name"))!;
        Assert.That(ex.Message, Does.Contain("No text box").And.Contain("First Name"));
    }

    [Test]
    public void RadioGroup_UnlinkedLabelAndWrongForOnSpan()
    {
        // Same pattern as the real page: the Gender label is unlinked and the Transgender span has for="female".
        Load("""
            <div>
              <label>Gender</label>
              <input type="radio" name="gender" value="Male"><span>Male</span>
              <input type="radio" name="gender" value="Female"><span>Female</span>
              <input type="radio" name="gender" value="Transgender"><span for="female">Transgender</span>
            </div>
            <div><label>Size</label><input type="radio" name="size" value="S"><span>Small</span></div>
            """);
        Assert.That(FieldLocator.FindRadioGroup(_driver, "Gender"), Has.Count.EqualTo(3));
        Assert.That(FieldLocator.FindRadioOption(_driver, "Gender", "Female").GetDomAttribute("value"), Is.EqualTo("Female"));
        Assert.That(FieldLocator.FindRadioOption(_driver, "Gender", "Transgender").GetDomAttribute("value"), Is.EqualTo("Transgender"));

        var ex = Assert.Throws<FieldResolutionException>(() => FieldLocator.FindRadioOption(_driver, "Gender", "Other"))!;
        Assert.That(ex.Message, Does.Contain("\"Male\"").And.Contain("\"Female\"").And.Contain("\"Transgender\""));
    }

    [Test]
    public void StateSelect_FoundByProximity_AndHasNoTextBox()
    {
        Load("""
            <form>
              <div><label>First Name</label><input type="text"></div>
              <div><label>State</label><select data-t="target"><option>A</option></select></div>
              <div><label>Country</label><input type="text"></div>
            </form>
            """);
        Assert.That(Tag(FieldLocator.FindSelect(_driver, "State")), Is.EqualTo("target"));
        Assert.Throws<FieldResolutionException>(() => FieldLocator.FindTextBox(_driver, "State"));
    }

    [Test]
    public void SingleTextBoxLabelledAsAnotherField_IsNotReturnedForState()
    {
        Load("""
            <div>
              <div><label>State</label><select><option>A</option></select></div>
              <div><label for="c1">Country</label><input id="c1" type="text"></div>
            </div>
            """);
        var ex = Assert.Throws<FieldResolutionException>(() => FieldLocator.FindTextBox(_driver, "State"))!;
        Assert.That(ex.Message, Does.Contain("labelled as").And.Contain("country"));
    }

    [Test]
    public void FirstNameWithoutItsInput_DoesNotReturnLastNameBox()
    {
        Load("""
            <div>
              <div><label>First Name</label></div>
              <div><label for="l1">Last Name</label><input id="l1" type="text"></div>
            </div>
            """);
        Assert.Throws<FieldResolutionException>(() => FieldLocator.FindTextBox(_driver, "First Name"));
    }

    [Test]
    public void NameHintAlone_DoesNotSelectAField()
    {
        Load("""<input type="text" name="first_name"><input type="text" name="other">""");
        var ex = Assert.Throws<FieldResolutionException>(() => FieldLocator.FindTextBox(_driver, "First Name"))!;
        Assert.That(ex.Message, Does.Contain("Only name/id hints matched"));
    }

    [Test]
    public void RandomizedIdsNamesAndClasses_StillFound()
    {
        Load("""
            <form>
              <div class="k5"><label>State</label><select id="s7" name="q3" data-t="state"><option>A</option></select></div>
              <div class="k6"><label>Gender</label>
                <input type="radio" name="q4" value="1"><span>Male</span>
                <input type="radio" name="q4" value="2" data-t="female"><span>Female</span></div>
              <div class="k4"><label>Last Name</label><input id="x8" name="q2" class="z1"></div>
              <div class="k3"><label>First Name</label><input id="x9" name="q1" class="z2" data-t="first"></div>
            </form>
            """);
        Assert.That(Tag(FieldLocator.FindTextBox(_driver, "First Name")), Is.EqualTo("first"));
        Assert.That(Tag(FieldLocator.FindRadioOption(_driver, "Gender", "Female")), Is.EqualTo("female"));
        Assert.That(Tag(FieldLocator.FindSelect(_driver, "State")), Is.EqualTo("state"));
    }
}
