# CloudQA form automation

A C# console program that opens the CloudQA practice form, fills in First Name, Gender and State, reads each value back from the page and prints PASS or FAIL per field. Fields are found by their visible label text ("First Name", "Gender", "State"), not by fixed ids, names, classes or XPath, so the program keeps working when the markup of the page changes. The page URL is configurable, so it can be pointed at a modified copy of the form.

How fields are found, and where that breaks, is described in [APPROACH.md](APPROACH.md).

## Requirements

- .NET 10 SDK
- Google Chrome

ChromeDriver is downloaded and matched to your Chrome by Selenium Manager, so the first run needs an internet connection.

## Setup

```bash
git clone https://github.com/thisisfaizanali/cloudqa-resilient-form-automation.git
cd cloudqa-resilient-form-automation
dotnet build
```

## Run against the live page

```bash
dotnet run --project src/FormAutomation
```

Chrome opens a visible window. For a run without a window:

```bash
dotnet run --project src/FormAutomation -- --headless true
```

Sample output:

```text
Page:  https://app.cloudqa.io/home/AutomationPracticeForm
Scope: form 1 of 7 on the page
PASS  First Name  expected "John", got "John"
PASS  Gender      expected "Male", got "Male"
PASS  State       expected "India", got "India"
Result: 3/3 passed
```

## Run against a modified page

Pass the page with `--url`. It must be an absolute `http`, `https` or `file` URL; anything else is rejected with an error and exit code 1.

A remote page:

```bash
dotnet run --project src/FormAutomation -- --headless true --url https://example.com/my-modified-form
```

A local HTML file (this one ships with the repo; use your own path):

```bash
dotnet run --project src/FormAutomation -- --headless true --url file:///C:/path/to/cloudqa-resilient-form-automation/variants/combined.html
```

On Linux or macOS the file URL looks like `file:///home/you/cloudqa-resilient-form-automation/variants/combined.html`.

## Configuration

Every setting has a default in `src/FormAutomation/appsettings.json` and can be overridden by an environment variable or a command line flag.

| Setting | `appsettings.json` key | Environment variable | Command line flag | Default |
|---|---|---|---|---|
| Page URL | `BaseUrl` | `CLOUDQA_BASEURL` | `--url` | `https://app.cloudqa.io/home/AutomationPracticeForm` |
| Hide the browser window | `Headless` | `CLOUDQA_HEADLESS` | `--headless` | `false` |
| Wait limit in seconds (page load and field lookup) | `TimeoutSeconds` | `CLOUDQA_TIMEOUTSECONDS` | `--timeout` | `30` |
| Value typed into First Name | `TestData:FirstName` | `CLOUDQA_TESTDATA__FIRSTNAME` | `--first-name` | `John` |
| Gender option to select | `TestData:Gender` | `CLOUDQA_TESTDATA__GENDER` | `--gender` | `Male` |
| State option to select | `TestData:State` | `CLOUDQA_TESTDATA__STATE` | `--state` | `India` |

Precedence, lowest to highest: `appsettings.json`, then environment variables, then command line flags. Nested keys in environment variables use a double underscore. Program flags go after the `--` that follows the `dotnet run` arguments, and take their value as the next argument, for example `-- --headless true`. .NET configuration matches keys case-insensitively, so `CLOUDQA_BASEURL` sets `BaseUrl`.

PowerShell:

```powershell
$env:CLOUDQA_BASEURL = "file:///C:/path/to/cloudqa-resilient-form-automation/variants/reordered.html"
$env:CLOUDQA_HEADLESS = "true"
dotnet run --project src/FormAutomation
```

bash:

```bash
CLOUDQA_BASEURL=file:///home/you/cloudqa-resilient-form-automation/variants/reordered.html CLOUDQA_HEADLESS=true dotnet run --project src/FormAutomation
```

Test values can be changed the same way, for example `-- --gender Female --state Canada`. The Gender and State values are matched against the visible option text, ignoring case and surrounding spaces.

## Output and exit codes

```text
Page:  <the URL that was opened>
Scope: <which part of the page the three fields were found in>
PASS|FAIL  <field>  expected "<value>", got "<value read back from the page>"
Result: <passed>/3 passed
```

- **PASS or FAIL** is per field. A field passes only when the value read back from the page equals the value that was set.
- **Scope** says where the fields were found: `form 2 of 7 on the page` (the only form containing all three fields), `3 forms contain all three fields, using the first`, `no single form, searched the whole document`, or `not resolved` when the fields could not be found.
- If a field cannot be found or is ambiguous, its line is FAIL and the detail lists the candidates that were considered. Because the three fields are located together, a missing field makes all three lines FAIL with `Form not resolved: ...`.
- **Exit code** is 0 only when all three fields pass. It is 1 otherwise, including invalid settings (such as a bad URL) and a browser that fails to start; those print only an error message.

## Tests

```bash
dotnet test
```

The suite uses NUnit and starts headless Chrome. It covers:

- **Text matching**: normalization of label text and id/name matching.
- **Locator fixtures**: small inline HTML pages for each way a label can relate to its control (label for, wrapping label, proximity, table layout, aria-label, hidden or look-alike decoys, randomized ids and names, ambiguity and not-found errors).
- **Page variants**: the full checks run against every file in `variants/`, with two sets of test data.
- **Settings**: URL validation.
- **Live page**: the checks and field round trips against the real CloudQA page. These need internet access.

## Page variants

`variants/` holds local copies of the practice form with the kinds of changes the page might receive. `baseline.html` is a copy of the live page; each other file changes something.

| File | What changed |
|---|---|
| `baseline.html` | Unchanged copy of the live page |
| `reordered.html` | Fields in a different order |
| `renamed-attributes.html` | All ids, names and classes replaced with meaningless values; `label for` values no longer match any id |
| `restructured.html` | Table layout for text fields, `fieldset` and `legend` with wrapping labels for Gender, State inside extra wrapper divs, labels `First name:` and `State *` |
| `swapped-ids.html` | The ids of the First Name and Last Name inputs swapped, labels untouched, so each `label for` points at the other box |
| `lookalikes.html` | Extra newsletter form with its own `First Name` field, a hidden `First Name` field, `Father's First Name`, `Middle Name`, `Preferred Contact Gender` and `State of Birth` |
| `combined.html` | Reordering, random attributes, restructuring and look-alikes together |
| `missing-state.html` | The State field removed; the run is expected to fail |

Run the console checks against all of them (headless, 5 second timeout) with PowerShell 7:

```powershell
pwsh scripts/run-variants.ps1
```

It exits 0 only if every variant passes except `missing-state`, which must fail.

## Project layout

```text
src/FormAutomation/
  Program.cs                  entry point: load settings, run checks, print results, exit code
  AppSettings.cs              settings from appsettings.json, environment and command line
  DriverFactory.cs            creates the ChromeDriver (headless option, page load timeout)
  FormChecks.cs               fills each field, reads it back, builds the PASS/FAIL results
  PracticeForm.cs             finds the form holding all three fields; set and read First Name, Gender, State
  Locating/FieldLocator.cs    finds a control from its label text by scoring candidates
  Locating/TextMatch.cs       text normalization and id/name matching
  Locating/FieldResolutionException.cs   error for not found or ambiguous, lists the candidates
  appsettings.json            default settings
tests/FormAutomation.Tests/   NUnit tests
variants/                     modified copies of the form
scripts/run-variants.ps1      runs the console checks against every variant
APPROACH.md                   approach and limitations
```
