# Approach and limitations

## The problem

The page under test may be modified before the program runs: fields can be reordered, their ids, names and classes changed, the markup around them rebuilt, or look-alike fields added. A script that hard-codes `#fname` or `//div[3]/input` breaks on the first such change.

The one thing a person relies on is what the page shows, so that is the only input here: the visible label text "First Name", "Gender" and "State". Everything else about the page is treated as unknown.

## How a field is found

All of this is in `Locating/FieldLocator.cs` and `Locating/TextMatch.cs`.

**Label text is normalized before any comparison**: lowercased, non-breaking spaces turned into spaces, runs of whitespace collapsed, and leading and trailing punctuation or symbols removed. `First Name *`, `FIRST NAME:` and `  First   Name ` all become `first name`.

**Candidates by kind.** For each field the program collects the controls that could hold it, ignoring any that are not displayed:

- First Name: `<input>` with no type or type text, email, tel, search or url.
- Gender: radio inputs, grouped by `name` (radios without a name are grouped by their parent element). A group is one candidate.
- State: `<select>`.

**Scoring.** Each candidate earns points from independent signals:

| Signal | Points |
|---|---|
| Explicit association: `label for`, a wrapping label, `aria-label`, `aria-labelledby`, or the `legend` of the enclosing `fieldset` (or `role="radiogroup"`), whose text equals the label | 100 |
| Proximity: an element showing exactly the label text, walked up to the first ancestor that contains a candidate, which contains exactly one | 80 |
| `placeholder` or `title` equal to the label | 40 |
| `name` or `id` that reads as the label (`first_name`, `firstName`, `first-name`) | 20 |

Rules around the scores:

- **Owned by another label.** If a control is explicitly tied to a different text (for example `label for` pointing at "Last Name"), it gets no proximity points. This stops a neighbouring field from being taken because it sits next to the right label.
- **Id-based links must agree with the layout.** `label for` and `aria-labelledby` are id references, as fragile as ids. If the walk from the referenced label (the same walk as proximity) reaches exactly one control and it is a different one than the link points to, the link is ignored: no 100 points, no ownership, and the reason `ignored label-for contradicted by layout` appears in error messages. So a page with swapped ids is read by what it shows. When the layout gives no single answer (several controls in one container), the link still decides. Wrapping labels, `aria-label`, legends and radiogroup aria are not id-based and are unchanged.
- **Minimum score of 40.** Ids and names alone (20) never select a field; they only break ties between candidates that already have a label, proximity, placeholder or title signal.
- **Ambiguity or no match is an error.** If the best score is below 40 the field is "not found". If two or more candidates tie for the best score the field is "ambiguous". Either way the program does not guess: it throws an error that lists every candidate with its tag, type, id, name, score and the reasons behind the score.

## Choosing the form

`PracticeForm.Open` looks at every displayed `<form>` and keeps the ones in which all three fields can be resolved. The page is polled until that succeeds or the timeout passes, so pages that build the form late still work.

- One matching form: it is used, and the Scope line says which one, for example `form 2 of 7 on the page`.
- Several matching forms: the first in document order is used, and the Scope line says so.
- No form matches: the whole document is searched as one scope (Scope line `no single form, searched the whole document`). If that fails too, the last error is reported.

This is why an unrelated form with its own "First Name" box, such as a newsletter sign-up, is not mixed up with the practice form: that form has no Gender or State.

Iframes and shadow roots are not entered. Selenium's element search does not cross into them, and entering them would mean guessing which embedded copy of a field is the real one. A form inside an iframe is reported as not found.

## Radio options and dropdown options

The Gender option and the State option are matched by their visible text (normalized), not by `value`, `for` or id.

- A radio's text is, in order: its wrapping label, the `label` whose `for` equals its id, the text that follows it up to the next form control, and only if all of those are empty, its `value`.
- The live page has `<span for="female">Transgender</span>`. The `for` on that span points at the wrong radio. It is ignored, and the text after the radio is used, so "Transgender" selects the Transgender radio.
- A dropdown option is picked by comparing the normalized text of each `<option>`. The live page pads some options with spaces (` Afganistan `), which does not matter.
- An option that does not exist, or a radio text that matches more than one radio, is an error that lists the available options.

## Verification

Each check sets a value and then reads it back from the page, rather than trusting that the action worked:

- **First Name**: the field is cleared, the value typed, and the DOM `value` property read back.
- **Gender**: the radio is clicked, the text of the radio that is now checked is read back, and exactly one radio in the group must be checked.
- **State**: the option is selected, and the text of the selected option is read back.

The read-back value and the expected value are compared after normalization, so the comparison ignores case, repeated spaces and punctuation at the edges. Elements are looked up again on every use instead of being cached, which avoids stale element errors when a page re-renders. One failing check never stops the other two.

## How it was tested

The brief lists the kinds of change the page may receive. Each one has a local copy of the form in `variants/` and is run through the same `FormChecks` code the console uses, with two sets of test data (`John`, `Male`, `India` and `Faizan`, `Transgender`, `Afganistan`). `scripts/run-variants.ps1` runs the console against all of them.

| Change in the brief | Variant | What handles it |
|---|---|---|
| Field order changed | `reordered.html` | Nothing depends on position. Fields are tied to their labels, not to their place in the DOM |
| Ids, names and classes changed | `renamed-attributes.html` | Ids, names and classes are not used to select a field (name and id only add 20 points). The `label for` values here point at ids that no longer exist, so the fields are found by proximity |
| Ids swapped between fields | `swapped-ids.html` | The First Name and Last Name ids are swapped, so each `label for` points at the other box. The links contradict the layout and are ignored; the box next to the label is chosen, and a test checks that the other box stays empty |
| Structure changed | `restructured.html` | Table cells (proximity walks up from the label to the first ancestor with a control), `fieldset` and `legend`, wrapping labels, extra wrapper divs, label text such as `First name:` and `State *` (normalization) |
| Look-alike fields added | `lookalikes.html` | A second form with its own "First Name" (form choice), a hidden "First Name" (not displayed, so ignored), "Father's First Name" and "Middle Name" (not an exact label match), "State of Birth" (a select owned by a different label) and "Preferred Contact Gender" (a second radio group with different label text) |
| All of the above at once | `combined.html` | All mechanisms together |
| A field removed (negative case) | `missing-state.html` | The run must fail: all three lines FAIL with `Form not resolved: No select found for label "State"` and the exit code is 1 |

`baseline.html` is an unchanged copy of the live form, showing the checks work offline before any change is applied. The locator itself is also covered by small inline fixtures in `FieldLocatorTests`, and the live page by `PracticeFormTests`.

## Limitations

- **The label text must stay.** The program relies on the visible text "First Name", "Gender" and "State". If "First Name" is renamed to "Given name" it will not find the field. It fails with an error instead of guessing, and there is no synonym list.
- **Labels and controls sharing one container.** If several labels and their inputs sit in one container with no per-field wrapper and no `label for`, aria or legend link, proximity gives no answer for any of them. The result is a "No text box found" error listing the candidates with score 0, not a wrong pick.
- **A decoy with the same label text.** Two visible fields with the same label text and the same score are reported as ambiguous. If a decoy scores higher than the real field (for example the decoy has a `label for` and the real one only proximity), the decoy is chosen.
- **Several forms with all three fields.** The first one in document order is used. This is reported in the Scope line but not treated as an error.
- **Radio option text still trusts `label for`.** When a radio has no wrapping label, its option text comes from the `label` whose `for` equals its id. If the radio ids are swapped on such a page, the wrong option could be selected. The contradiction rule above applies to finding the group, not to option text.
- **The three fields are resolved as a unit.** If one is missing or ambiguous, the form is not resolved and all three checks fail with `Form not resolved`, even though the other two fields might exist.
- **Iframes and shadow DOM** are not searched.
- **Only native controls.** First Name must be an `<input>` of a text-like type, Gender must be radio inputs and State must be a real `<select>`. A custom dropdown built from divs is not recognized.
- **Case-insensitive matching covers ASCII letters only** in the XPath that finds label elements.
- **Chrome only.** The driver is `ChromeDriver`.
- **Many WebDriver round trips per lookup.** Each candidate is inspected with several calls. This is fast enough for a form of this size and slower on very large pages.
- **A run that cannot find the fields waits the full timeout** (30 seconds by default, `--timeout` to change it) before failing.
- **The comparison is not case sensitive.** Because the read-back value is normalized, a field that changes the case of the typed text would still pass.
