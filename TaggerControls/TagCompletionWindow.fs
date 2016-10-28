(* Copyright 2014 FSharpN00b.
This file is part of Tagger.

Tagger is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

Tagger is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Tagger.  If not, see <http://www.gnu.org/licenses/>.

Tagger uses AvalonEdit, which is copyright 2014 AlphaSierraPapa for the SharpDevelop Team under the terms of the MIT License. For more information see www.avalonedit.net. *)

namespace TaggerControls

// CancelEventArgs, String
open System
// File
open System.IO
// Regex
open System.Text.RegularExpressions
// DependencyPropertyChangedEventArgs, Window
open System.Windows
// ComboBox
open System.Windows.Controls
// *Args
open System.Windows.Input

// point_to_dpi_safe_point
open GeneralHelpers

// TODO2 Consider a constructor that throws if the value starts with the tag symbol when it should not, and vice versa.

/// <summary>A tag that is preceded by the tag symbol.</summary>
type TagWithSymbol = TagWithSymbol of string with
(* The default ToString implementation does not return the inner string, so we extract it with a pattern match. *)
    override this.ToString () = match this with | TagWithSymbol s -> s
/// <summary>A tag that is not preceded by the tag symbol.</summary>
type TagWithoutSymbol = TagWithoutSymbol of string with
    override this.ToString () = match this with | TagWithoutSymbol s -> s

(* F# does not have static types. *)
/// <summary>Helper type for working with tags.</summary>
type TagInfo () =

(* Static member values. *)

/// <summary>The tag symbol.</summary>
    static let mutable _tag_symbol = ""

(* Note we restrict the tag symbol to characters that do not match \w, so we can distinguish the tag symbol from the tag. *)
/// <summary>A regular expression used to search for tags. Does not include the tag symbol. Does not exclude tags that are preceded by links.</summary>
    static let _tag_pattern_no_symbol = "\w+"

(* Note temporary one-way tags are ignored by TagInfo.find_tags_without_symbol. This means they are not found when the user right-clicks and selects "Find All Tags in Project". They are not added to the Tag MRU when they are created, or when the user opens a project, because the user should not be able to select them from the Add Tag dialog. *)
(* The Literal attribute lets us use this string as the format parameter to sprintf. *)
/// <summary>The format for a temporary one-way tag.</summary>
    [<Literal>]
    static let _temp_tag_format_with_symbol = "%szzTemp_%d_%d"

(* Notes about regex that we learned while writing this expression.

Atomic groups are non-capturing.
http://www.rexegg.com/regex-disambiguation.html

The characters in non-captured groups still appear in the result. This means the tag appeared in the result, which we didn't want. Using a lookbehind for the tag does not seem to work, possibly because the tag symbol is consumed by the match of non-whitespace characters, even though that match is non-greedy.
https://stackoverflow.com/questions/3926451/how-to-match-but-not-capture-part-of-a-regex
"The only way not to capture something is using look-around assertions. ... [E]ven with non-capturing groups (?:...) the whole regular expression captures their matched contents."
TODO3 Try RegexOptions.ExplicitCapture next time we want to match something, but not have it appear in the result, and can't use a lookbehind.
http://msdn.microsoft.com/en-us/library/system.text.regularexpressions.regexoptions.aspx

Variable-length lookbehind works in .NET, but not in all regex implementations.
http://oylenshpeegul.typepad.com/blog/2011/12/variable-length-look-behind-in-regular-expressions.html
http://www.regular-expressions.info/lookaround.html
"[M]ost regex flavors do not allow you to use just any regex inside a lookbehind, because they cannot apply a regular expression backwards. ... Many regex flavors, including those used by Perl and Python, only allow fixed-length strings."
https://stackoverflow.com/questions/2130037/is-it-better-to-use-look-behind-or-capture-groups
"[T]he lookbehind approach is not as flexible; it falls down when you start dealing with variable-length matches."
Notes about various group types, such as lookarounds, conditional groups and atomic groups.
http://www.rexegg.com/regex-disambiguation.html

.NET allows multiple captures of the same named group.
http://oylenshpeegul.typepad.com/blog/2011/10/regular-expressions-with-multiple-named-captures.html
*)

(* Note we cannot use the following characters in regex comments.
1. %d, because this pattern is used as the format parameter to sprintf.
2. ( and ), because ) closes a comment.
3. A double quote, because it closes the string.
*)
(* The Literal attribute lets us use this string as the format parameter to sprintf. *)
/// <summary>The pattern to find a tag. Excludes tags that are preceded by links.</summary>
    [<Literal>]
    static let _find_tag_with_symbol_pattern = @"
(?# Negative lookbehind.)
(?<!
(?# Match 'http://' or 'https://'.)
    https?://
(?# Non-greedy match zero or more non-whitespace characters.)
    \S*?
)
(?# Non-capture group.)
(?:
(?# Match the tag symbol.)
    %s
(?# Match the tag.)
    (?<tag>%s)
)"

// TODO2 Replace \n with Environment.NewLine everywhere.

(* We do not know how to combine the negative lookbehind to exclude links with the quantifier to find the tag at the specified index. So this regex should be used only with strings that have already been checked for links. *)
(* The pattern:
.{0,%d}
is a greedy match from 0 to d characters. We are given an offset in a line of text and we want to see if there is a tag at that offset. We want to skip over, at most, enough characters to reach the offset, but to stop skipping when we reach either the tag symbol or the first character of the word that contains the offset. For example, if the line is:
012345678901
Lorem #ipsum
and the offset is 10, we only want to skip 6 characters because the tag starts at offset 6.
Note this has a weakness. If the line is:
0123456789012345678
#Lorem ipsum #dolor
and the offset is 10, the pattern matches the tag at offset 0, rather than fail as we would like. See the comments for position_to_tag for information on how we correct this. *)
    [<Literal>]
/// <summary>The pattern to find a tag. Does not exclude tags that are preceded by links.</summary>
    static let _position_to_tag_with_symbol_pattern = @"
(?# Match the start of the string.)
^
(?# Match 0 to the specified number of characters.)
(?<skipped_chars>.{0,%d})
(?# Match the tag symbol.)
%s
(?# Match the tag.)
(?<tag>%s)"

/// <summary>The pattern to match a temporary one-way tag.</summary>
    static let _temp_tag_no_symbol_pattern = "zzTemp_(?<file_index>[0-9]+)_(?<line_number>[0-9]+)"

(* Static methods. *)

(* We do not need to regex escape the tag symbol here. *)
/// <summary>If the text (1) starts with the tag symbol, return it; otherwise, return the text with the tag symbol added.</summary>
    static member add_tag_symbol_to_start (tag : TagWithoutSymbol) : TagWithSymbol =
        let tag_ = tag.ToString ()
        if tag_.StartsWith _tag_symbol then TagWithSymbol tag_ else sprintf "%s%s" _tag_symbol tag_ |> TagWithSymbol

(* We need to regex escape the tag symbol here. *)
/// <summary>If the text (1) starts with the tag symbol, return the text with the tag symbol removed; otherwise, return the text.</summary>
    static member remove_tag_symbol_from_start (tag : TagWithSymbol) : TagWithoutSymbol =
        Regex.Replace (tag.ToString (), sprintf "^%s" (Regex.Escape _tag_symbol), "") |> TagWithoutSymbol

(* We do not need to ignore temporary one-way tags here. *)
(* We need to regex escape the tag symbol here. *)
/// <summary>Return true if tag (1) is valid; otherwise, false. (1) must include the tag symbol. Excludes tags that are preceded by links.</summary>
    static member whole_string_is_valid_tag_with_symbol (tag : TagWithSymbol) = Regex.IsMatch (tag.ToString (), sprintf "^%s%s$" (Regex.Escape _tag_symbol) _tag_pattern_no_symbol)

// TODO1 Bug, select multiple URLs and right click, some URLs are listed twice in the open url context menu.
// TODO1 Bug, right clicking on tag sometimes just opens normal line context menu.
// TODO1 Bug, selecting too large a block of text causes hang if you mouse over Find in Project.

// TODO1 Need to review naming conventions. find_tag_pattern_with_symbol is confusing because it does not include the symbol in the result. On the other hand, find_tags_with_symbol does include the symbol in the result. And note that no pattern includes the symbol in the result, since we can always add it back later.

(* We ignore temporary one-way tags here. *)
/// <summary>Return all tags in text (1). Do not include the tag symbol in each tag. Exclude tags that are preceded by links.</summary>
    static member find_tags_without_symbol text : TagWithoutSymbol list =
        Regex.Matches (text, TagInfo.find_tag_pattern_with_symbol, RegexOptions.IgnorePatternWhitespace)
        |> Seq.cast<Match>
        |> Seq.map (fun match_ -> match_.Groups.["tag"].Value |> TagWithoutSymbol)
(* Ignore temporary one-way tags. We do not handle this in _tag_pattern_no_symbol because that is also used in _position_to_tag_with_symbol_pattern to detect whether the user right-clicked on a tag, and we do want that to find temporary one-way tags. We also do not handle this in _find_tag_with_symbol_pattern because we are reluctant to make that regex more complex. *)
        |> Seq.filter (fun tag -> tag |> TagInfo.add_tag_symbol_to_start |> TagInfo.is_temp_tag = None)
        |> Seq.toList

(* We ignore temporary one-way tags here. *)
/// <summary>Return all tags in text (1). Include the tag symbol in each tag. Exclude tags that are preceded by links.</summary>
    static member find_tags_with_symbol text : TagWithSymbol list =
(* If the caller wants the results to include the tag symbol, add it. *)
(* We do not need to regex escape the tag symbol here. *)
        text |> TagInfo.find_tags_without_symbol |> List.map TagInfo.add_tag_symbol_to_start

(* We do not need to ignore temporary one-way tags here. *)
/// <summary>Find out whether (1) starts with text that is a valid tag. Excludes tags that are preceded by links. If (1) starts with text that is a valid tag, return a tuple. (R1) The text that is a valid tag. (R2) The text that follows (R1) and is not a valid tag, if any. If (1) does not start with text that is a valid tag, return None.</summary>
    static member starts_with_valid_tag_no_symbol text : (TagWithoutSymbol * string) option =
        let result = Regex.Match (text, TagInfo.starts_with_valid_tag_no_symbol_pattern, RegexOptions.IgnoreCase)
        if result.Success then Some (result.Groups.["tag"].Value |> TagWithoutSymbol, result.Groups.["rest"].Value)
        else None

(* We do not need to ignore temporary one-way tags here. *)
(* We do not know how to combine the negative lookbehind to exclude links with the quantifier to find the tag at the specified index. So this regex should be used only with strings that have already been checked for links. *)
/// <summary>Find the tag at index (2) in text (1). Does not exclude tags that are preceded by links. If we succeed, return the tag; otherwise, return None.</summary>
    static member position_to_tag_with_symbol (text : string) index : TagWithoutSymbol option =
(* If the index is not valid, or the character at that index is whitespace, return None. *)
        if index >= text.Length || text.[index] |> Char.IsWhiteSpace then None
        else
            let result = Regex.Match (text, TagInfo.position_to_tag_with_symbol_pattern index, RegexOptions.IgnoreCase |||RegexOptions.IgnorePatternWhitespace)
            if result.Success then
                let tag = result.Groups.["tag"].Value
(* If the text contains a tag that ends before the index, and the index is not within another tag, position_to_tag_pattern matches the tag, rather than fail as we would like. See the comments for _position_to_tag_pattern for more information.
To correct this, check that index is within the tag matched by the pattern. To do so, get the number of characters matched by the pattern, add it to the length of the tag symbol and the tag, and check that the total length is enough to include the index.
We use the less than operator because a length of 1 includes the character at index 0, but not index 1. *)
                if index < result.Groups.["skipped_chars"].Value.Length + _tag_symbol.Length + tag.Length then tag |> TagWithoutSymbol |> Some
                else None
            else None

/// <summary>Find the text at index (2) in text (1). Return the text if we succeed; otherwise, None.</summary>
    static member position_to_text_bordered_by_whitespace (text : string) index =
(* If the index is not valid, or the character at that index is whitespace, return None. *)
        if index >= text.Length || text.[index] |> Char.IsWhiteSpace then None
        else
(* Helper function. Return true if character (1) is not whitespace; otherwise, false. *)
            let is_not_whitespace c = Char.IsWhiteSpace c = false
(* Get characters from index until the next whitespace. *)
            let part_2 = text.Substring index |> Seq.takeWhile is_not_whitespace |> Seq.toArray
(* Get characters from (index - 1) until the previous whitespace. *)
            let part_1 = text.Substring (0, index) |> Seq.toArray |> Array.rev |> Seq.takeWhile is_not_whitespace |> Seq.toArray |> Array.rev
(* We convert the character sequences to arrays so they can be passed to the string constructor. *)
            new String (Array.append part_1 part_2) |> Some

/// <summary>Return the words in text (1).</summary>
    static member find_words text = Regex.Matches (text, "\w+") |> Seq.cast<Match> |> Seq.toList |> List.map (fun match_ -> match_.Value)

/// <summary>Return the links in text (1).</summary>
    static member find_links text = Regex.Matches (text, "https?://\S+") |> Seq.cast<Match> |> Seq.toList |> List.map (fun match_ -> match_.Value)

/// <summary>Return a temporary one-way tag for file index (1) and line number (2).</summary>
    static member format_temp_tag file_index line_number : TagWithSymbol =
        sprintf (Printf.StringFormat<string->int->int->string> (_temp_tag_format_with_symbol)) TagInfo.tag_symbol file_index line_number |> TagWithSymbol

/// <summary>Return true if tag (1) is a temporary one-way tag.</summary>
    static member is_temp_tag (tag : TagWithSymbol) =
(* _temp_tag_no_symbol_pattern does not depend on whether the tag has a tag symbol or not. *)
        let result = Regex.Match (tag.ToString (), _temp_tag_no_symbol_pattern, RegexOptions.IgnoreCase)
        if result.Success then
            match result.Groups.["file_index"].Value |> Int32.TryParse, result.Groups.["line_number"].Value |> Int32.TryParse with
            | (true, file_index), (true, line_number) -> Some (file_index, line_number)
            | _ -> None
        else None

(* Static private properties. *)

/// <summary>Get a regular expression to find a tag. The tag symbol is not included in the pattern. The pattern tries to match part of the string, starting at the beginning. Excludes tags that are preceded by links.</summary>
    static member private starts_with_valid_tag_no_symbol_pattern with get () = sprintf "^(?<tag>%s)(?<rest>.*)" _tag_pattern_no_symbol

(* Printf.StringFormat lets you use a string value instead of a string literal as the format.
1. The string value must be marked with the Literal attribute. This seems to mean it must be declared in at least the scope of a type, not in the scope of a function.
2. Printf.StringFormat must be given, as a type parameter, the type of a function that takes the values to format and returns a string, which is the output. For example, if the format is "%d%f", the type parameter is int->float->string. *)
(* We need to regex escape the tag symbol here. *)
/// <summary>Get a regular expression to find a tag. Excludes tags that are preceded by links.</summary>
    static member private find_tag_pattern_with_symbol with get () = sprintf (Printf.StringFormat<string->string->string> (_find_tag_with_symbol_pattern)) (Regex.Escape _tag_symbol) _tag_pattern_no_symbol

(* We need to regex escape the tag symbol here. *)
(* We do not know how to combine the negative lookbehind to exclude links with the quantifier to find the tag at the specified index. So this regex should be used only with strings that have already been checked for links. *)
/// <summary>Get a regular expression to find a tag. Does not exclude tags that are preceded by links. (1) The index in the string at which to try to find the tag.</summary>
    static member private position_to_tag_with_symbol_pattern with get index =
        sprintf (Printf.StringFormat<int->string->string->string> (_position_to_tag_with_symbol_pattern)) index TagInfo.regex_escaped_tag_symbol _tag_pattern_no_symbol

(* Static properties. *)

/// <summary>Used to mark a tag.</summary>
    static member tag_symbol
        with get () = _tag_symbol
        and set value = do _tag_symbol <- value

/// <summary>The tag symbol for use in a regex.</summary>
    static member regex_escaped_tag_symbol with get () = _tag_symbol |> Regex.Escape

/// <summary>The characters allowed in a tag.</summary>
    static member tag_pattern_no_symbol with get () = _tag_pattern_no_symbol

(* We had to subclass ComboBox and override OnApplyTemplate to get access to the internal TextBox. *)
/// <summary>The combo box used in the tag completion window.</summary>
type TagCompletionComboBox () as this =
    inherit ComboBox ()

(* Member values. *)

/// <summary>The items to show in the combo box drop down list if no filtering is applied.</summary>
    let mutable _base_items : string list = []
/// <summary>The function that determines whether a combo box item matches text entered by the user. (1) The combo box item text. (2) The text entered by the user. Return true if there is a match; otherwise, false.</summary>
    let mutable _find = fun (source : string) target -> source.IndexOf (target, StringComparison.OrdinalIgnoreCase) > -1
(* Note that _textbox.Text is not connected to this.Text. Once a value is assigned to _textbox, always use _textbox.Text, as this.Text seems to be ignored. *)
/// <summary>The text box part of the combo box.</summary>
    let mutable (_textbox : TextBox) = null

(* Events. *)

/// <summary>The user has finished entering text or has selected an item. (1) The part of the text that matches the pattern. (2) The part of the text that does not match the pattern.</summary>
    let _commit = new Event<TagWithoutSymbol * string> ()
/// <summary>The user has canceled.</summary>
    let _cancel = new Event<unit> ()
/// <summary>A general non-critical error occurred. (1) The error message.</summary>
    let _general_error = new Event<string> ()

(* Helper functions. *)

(* This is currently not used. *)
(* We convert the return value from a sequence to a list because it seems that a sequence implicitly contains references whereas a list contains values. If we return a sequence and the caller then calls this.Items.Clear (which happens in filter), the sequence items become non-valid. Converting the sequence to a list causes it to be evaluated and the references converted to values. *)
/// <summary>Convert the combo box items to a list of strings. Return the list.</summary>
    let get_items () = this.Items |> Seq.cast<ComboBoxItem> |> Seq.map (fun item -> item.Content.ToString ()) |> Seq.toList

(* Event handlers. *)

(* We handle input to the tag completion window as follows:
Esc                 Cancel
Tab                 Commit
Space               Commit
Enter               Commit
(Non-valid input)   Commit
(LostKeyboardFocus) Cancel
(Select an item)    Commit
*)

(* We handle the preview version of this event because the default handler interferes with ours. *)
/// <summary>Handle the PreviewMouseLeftButtonUp event on a combo box item. (1) The combo box item text. Return unit.</summary>
    let preview_mouse_left_up_handler text =
(* Signal that the user has selected an item. *)
        do (text |> TagWithoutSymbol, String.Empty) |> _commit.Trigger

(* We have the following key/text input handlers.
ComboBox.PreviewKeyDown - If the user pressed up or down arrow, we change the selection. If the user pressed Esc, Tab, Space, or Enter, we close the TCW. This handler does not deal with text.
ComboBox.PreviewTextInput - If the user has selected a tag and then enters text that isn't valid in a tag, we close the TCW, insert the currently selected tag, and insert the non-tag text after it. This handler only deals with the case where the user has selected an item, not when the user has entered text in the text box.
TextBox.TextChanged - We update the items in the combo box based on the text in the text box. This handler only deals with the case where the user has entered text in the text box.
*)

(* We handle the preview version of this event because the default handler interferes with ours. We might mark this event handled, depending on the arguments. *)
/// <summary>Handle the KeyDown event on the combo box. (1) Event args. Return unit.</summary>
    let preview_key_down_handler (args : KeyEventArgs) =
(* If the user pressed Esc... *)
        if args.Key = Key.Escape then
            do
(* Mark the event handled. *)
                args.Handled <- true
(* Fire the cancel event. *)
                _cancel.Trigger ()
(* If the user pressed the down arrow key, and the combo box has items, and an item isn't already selected... *)
        else if args.Key = Key.Down && this.HasItems && this.SelectedIndex = -1 then
            do
(* Open the drop down. *)
                this.IsDropDownOpen <- true
(* Select the first item. *)
                this.SelectedIndex <- 0
(* Set the focus to the combo box, and therefore the drop down list. *)
                this.Focus () |> ignore
(* Mark the event handled. *)
                args.Handled <- true
(* If the user pressed the up arrow key and the first item is selected... *)
        else if args.Key = Key.Up && this.SelectedIndex = 0 then
            do
(* Clear the selection. *)
                this.SelectedIndex <- -1
(* Close the drop down. We cannot remove the highlight from the first item except by doing this. We tried setting SelectedItem to null, setting SelectedValue to null, and calling InvalidateVisual. *)
                this.IsDropDownOpen <- false
(* Set the focus back to the text box. *)
                _textbox.Focus () |> ignore
(* Mark the event handled. *)
                args.Handled <- true
(* Otherwise, if the user pressed Tab, Space, or Enter... *)
        else if
            args.Key = Key.Tab
            || args.Key = Key.Space
            || args.Key = Key.Enter
            then
(* Mark the event handled, so the key isn't passed on to the editor after we close the tag completion window. *)
            do args.Handled <- true
(* If an item is selected, send the item text to the commit event. *)
            match this.SelectedItem with
            | :? ComboBoxItem as cbi -> do (cbi.Content.ToString () |> TagWithoutSymbol, String.Empty) |> _commit.Trigger
(* This correctly matches null. *)
(* Otherwise, send the text box text to the commit event. *)
            | _ -> do (_textbox.Text |> TagWithoutSymbol, String.Empty) |> _commit.Trigger 

(* If the user enters text that is not a valid tag, we append it to the tag, no matter where the cursor is. It seems unlikely that the user will insert text that is not a valid tag into the middle of a tag while using the tag completion window. *)
(* We handle the preview version of this event so we can change the input before it reaches the control. We might mark this event handled, depending on the arguments. *)
/// <summary>Handle the PreviewTextInput event on the combo box. (1) Event args. Return unit.</summary>
    let preview_text_input_handler (args : TextCompositionEventArgs) =
(* Helper function to fire the commit event. (1) Text that is a valid tag. (2) Text that is not a valid tag. *)
        let commit tag rest =
(* If the user has selected an item... *)
            match this.SelectedItem with
            | :? ComboBoxItem as cbi ->
                do
(* Mark the event handled. *)
                    args.Handled <- true
(* Insert the selected item text, the valid text, and the non-valid text. *)
                    (sprintf "%s%s" (cbi.Content.ToString ()) (tag.ToString ()) |> TagWithoutSymbol, rest) |> _commit.Trigger
(* If the user has not selected an item, then do not mark the event handled. We'll deal with the text in text_changed_handler. *)
            | _ -> ()
(* If the text is empty, stop. *)
        if args.Text.Length = 0 then ()
(* See if the text starts with text that is a valid tag. *)
        match args.Text |> TagInfo.starts_with_valid_tag_no_symbol with
(* If all of the text is a valid tag, then do not mark the event handled. We'll deal with the text in text_changed_handler. *)
        | Some (tag, "") -> ()
(* If some of the text is a valid tag and some is not, send each to the commit event. *)
        | Some (tag, rest) -> commit tag rest
(* If none of the text is a valid tag, send it to the commit event. *)
        | None -> commit "" args.Text

(* We do not mark this event handled. *)
/// <summary>Handle the TextChanged event of the text box. (1) Event args. Return unit.</summary>
    let text_changed_handler (args : TextChangedEventArgs) =
(* Filter the combo box items based on the text. *)
        do this.filter _textbox.Text
(* If the text is empty, close the drop down list. *)
        if _textbox.Text.Length = 0 then do this.IsDropDownOpen <- false
        else
(* If the text box is not empty, see if some or all of the text is a valid tag. *)
            match _textbox.Text |> TagInfo.starts_with_valid_tag_no_symbol with
(* If all of the text is a valid tag, it means the user is not done entering the tag yet. If the combo box still has items, open the drop down list; otherwise, close it. *)
            | Some (tag, "") -> do this.IsDropDownOpen <- this.HasItems
(* If some of the text is a valid tag and some is not, it means the user entered non-tag text into the text box. We take that as a signal to commit the tag. *)
            | Some (tag, rest) -> do (tag, rest) |> _commit.Trigger
(* If none of the text is a valid tag, it means the user entered non-tag text at the beginning of the text box, so cancel. *)
            | None -> do _cancel.Trigger ()

(* This calls preview_mouse_left_up_handler and so must be defined after it. *)
/// <summary>Add the items (1) to the combo box list. (2) True to clear the text box part of the combo box; otherwise, false. Return unit.</summary>
    let add_items items clear_textbox =
(* Clear the text box if requested. We check the text box for null here because this method might be called before OnApplyTemplate (where the text box is extracted from the combo box). *)
        if clear_textbox && _textbox <> null then do _textbox.Text <- String.Empty
        do
(* Clear the combo box list. *)
            this.Items.Clear ()
(* Add the items to the combo box list. *)
            items |> List.iter (fun item ->
(* Create a combo box item. *)
                let cbi = new ComboBoxItem ()
                do
(* Set properties. *)
                    cbi.Content <- item
(* Add event handlers. *)
(* We handle the preview version of this event, because the default handler interferes with ours. *)
                    cbi.PreviewMouseLeftButtonUp.Add <| fun _ -> do preview_mouse_left_up_handler item
(* Add the item to the combo box. *)
                    cbi |> this.Items.Add |> ignore
                )

(* Constructor. *)

    do
(* Add event handlers. *)
        this.PreviewKeyDown.Add preview_key_down_handler
        this.PreviewTextInput.Add preview_text_input_handler
(* We add the TextChanged handler in OnApplyTemplate. *)
(* We add the PreviewMouseLeftButtonUp handler in add_items. *)

(* Set properties. *)
(* We considered setting the StaysOpenOnEdit property to true. However, that's meant for use with the combo box's default matching function (which seems to be String.StartsWith). We implement a custom matching function. To do that, when the user edits the text box, we rebuild the combo box Items list, which means that the drop down must be closed and re-opened. *)
(* The default value of IsTextSearchCaseSensitive is false, which is fine. *)
        this.MinWidth <- 100.0
(* We want the IsEditable property set to true and the IsReadOnly property set to false. See:
http://msdn.microsoft.com/en-us/library/system.windows.controls.combobox%28v=vs.110%29.aspx
Both properties are false by default.
*)
        this.IsEditable <- true

(* Methods. *)

(* Originally, we exposed the text box's Focus method through a method on the combo box. That led to odd behavior. The first time we showed the TCW, the combo box would get mouse capture. Whenever we showed the TCW after that, the editor would retain mouse capture. This seems to have been caused by calling TextBox.Focus through a method on the combo box rather than within the combo box.
Originally, the tag completion window contained a regular combo box. When we showed the window with Show, WPF set the focus to the text box part of the combo box, so the user could begin entering text, which is what we want.
However, when we showed the window with ShowDialog, WPF set the focus to the combo box, not the text box, so the user could not enter text without clicking on the text box. We could not find out how to set the focus to the text box. So we went back to showing the window with Show.
Now that we extract the text box from the combo box, WPF again sets the focus to the combo box and not the text box, which we do not want. We believe that extracting the text box from the combo box means that the text box is no longer part of the combo box's visual tree. Anyway, now that we can access the text box, we set focus to it manually. *)
(* We extract the text box from the combo box so we can set focus to it (as opposed to the drop down list part of the combo box), get or set the cursor index, and detect when the text changes. *)
/// <summary>Override FrameworkElement.OnApplyTemplate. Return unit.</summary>
    override this.OnApplyTemplate () =
        if _textbox = null then
            do
(* Get the text box part of the combo box. *)
                _textbox <-
                    match this.GetTemplateChild "PART_EditableTextBox" with
(* This does not match null. *)
                    | :? TextBox as textbox -> textbox
(* Note we tried firing this event for debugging purposes. It caused an unhandled exception that stopped the application. As a result, the event is not shown in a message box as it should be (see logconfig.xml, PaneController > EditorGeneralError), but it is written to the log file. *)
(* If the result is null, fire the error event. This should never happen. *)
                    | _ ->
                        do "TagCompletionComboBox.OnApplyTemplate: GetTemplateChild failed to get TextBox." |> _general_error.Trigger
                        null    
(* Add event handlers. *)
                _textbox.TextChanged.Add text_changed_handler

/// <summary>Update the combo box to show only items that match text (1). If (1) is empty, restore all items to the combo box. Return unit.</summary>
    member this.filter (text : string) =
        let items =
(* If the text is empty, get all items. *)
            if text.Length = 0 then _base_items
(* Otherwise, filter out all items that do not match the text. *)
            else _base_items |> List.filter (fun item -> this.find item text)
(* Add the items to the combo box list. Do not clear the text box. *)
        do add_items items false

(* This is not currently used. *)
/// <summary>Return the index of the first item in the combo box list that matches text (1). If no items match, return None.</summary>
    member this.find_index text = _base_items |> Seq.tryFindIndex (fun item -> this.find item text)

/// <summary>Prepare the combo box for the user to enter or select another tag. Return unit.</summary>
    member this.clear () =
        do
            _textbox.Text <- System.String.Empty
            this.IsDropDownOpen <- false
            _textbox.Focus () |> ignore

(* Properties. *)

/// <summary>The items to show in the combo box drop down list if no filtering is applied.</summary>
    member this.base_items
        with set value =
            do
                _base_items <- value
(* Add the items to the combo box list. Clear the text box. *)
                add_items _base_items true

/// <summary>The text to show in the text box.</summary>
    member this.base_text
        with set value =
            do
(* Put the text in the text box. *)
                _textbox.Text <- value
(* Place the cursor at the end of the text. *)
                _textbox.CaretIndex <- _textbox.Text.Length

/// <summary>The function that determines whether a combo box item matches text entered by the user. (1) The combo box item text. (2) The text entered by the user. Return true if there is a match; otherwise, false.</summary>
    member this.find
        with get () = _find
        and set value = do _find <- value

(* We had to expose this so we could set focus to the text box manually from outside this type. *)
/// <summary>The text box.</summary>
    member this.textbox with get () = _textbox

(* Events. *)

/// <summary>The user is finished entering text or has selected an item.</summary>
    member this.commit = _commit.Publish
/// <summary>The user has canceled.</summary>
    member this.cancel = _cancel.Publish
/// <summary>A general non-critical error occurred. (1) The error message.</summary>
    member this.general_error = _general_error.Publish

/// <summary>When the user enters the tag symbol, this window shows a combo box with the existing tags.</summary>
type TagCompletionWindow () =
/// <summary>The window.</summary>
    let _win = new Window ()
/// <summary>The combo box that shows the matches.</summary>
    let _matches = new TagCompletionComboBox ()
/// <summary>A general non-critical error occurred. (1) The error message.</summary>
    let _general_error = new Event<string> ()

(* Events. *)
/// <summary>The user has finished entering a tag or has selected a tag. (1) The tag the user entered or selected. (2) Additional text the user entered, if any.</summary>
    let _commit = new Event<TagWithoutSymbol * string> ()

(* Event handlers. *)

/// <summary>Handle the Window.Activated event. Return unit.</summary>
    let activated_handler _ =
        do
(* Set focus to the text box part of the combo box. Previously, we did this in TagCompletionComboBox.is_visible_changed_handler, but that did not work consistently. Then we did it after calling Window.Show. That worked. However, it would not work if we needed to call Window.ShowDialog instead of Window.Show. By setting the focus to the text box in the handler for the Activated event, we can call Window.ShowDialog if we want, as we do in AddTagWindow.ShowDialog. *)
            _matches.textbox.Focus () |> ignore
    
/// <summary>Handle the commit event from the TagCompletionComboBox. (1) A tuple. (1) The tag to insert in the current document. (2) Additional text to insert after the tag. Return unit.</summary>
    let commit_handler (tag_and_text : TagWithoutSymbol * string) =
(* Hide the window. We do this first because, if the text is a tag symbol, the editor shows the window again. We do not want the window to be shown and then immediately hidden again. *)
        do _win.Hide ()
        match tag_and_text with
(* If the tag is not empty, notify the editor that the user entered or selected a tag. The editor in turn notifies the TagController to add the tag to the Tag MRU (see TagController.tag_selected_handler). *)
        | tag, text when (tag.ToString ()).Length > 0 -> do (tag, text) |> _commit.Trigger
        | _ -> ()

/// <summary>Hide the window.</summary>
    let cancel_handler () = do _win.Hide ()

(* Editor has a reference to an instance of this dialog. If we close it, that reference becomes invalid. So we hide it instead. See:
http://msdn.microsoft.com/en-us/library/system.windows.window.hide%28v=vs.110%29.aspx
"If you want to show and hide a window multiple times during the lifetime of an application, and you don't want to re-instantiate the window each time you show it, you can handle the Closing event, cancel it, and call the Hide method. Then, you can call Show on the same instance to re-open it." *)
/// <summary>Handle the Closing event from the Window. If the user closes the window, hide it instead. (1) The event args. Return unit.</summary>
    let closing_handler (args : ComponentModel.CancelEventArgs) =
        do
(* Cancel the window closing. *)
            args.Cancel <- true
(* Hide the window. *)
            cancel_handler ()

(* Since we extract the text box from the combo box, it seems the text box is no longer part of the combo box visual tree. However, the window's IsKeyboardFocusWithin property seems to include both the combo box and text box. *)
/// <summary>Handle the IsKeyboardFocusWithinChanged event from the Window. (1) Event args. Return unit.</summary>
    let is_keyboard_focus_within_changed_handler (args : DependencyPropertyChangedEventArgs) =
        match args.NewValue with
        | :? System.Boolean as value when value = false -> do cancel_handler ()
        | _ -> ()

(* Method helpers. *)

/// <summary>Position the tag completion window. (1) The top and left point at which to show the window. (2) The bottom point at which to show the window. Return unit. Precondition: the tag completion window is visible.</summary>
    let draw_window top_point bottom_point =
(* Convert the points to DPI-safe versions. *)
        let top_point_, bottom_point_ = top_point |> point_to_dpi_safe_point, bottom_point |> point_to_dpi_safe_point
        do
(* Set the top and left of the window to the requested point. *)
            _win.Left <- top_point_.X
            _win.Top <- top_point_.Y
(* Set the height of the combo box to the height of the line it is shown on. The window is sized accordingly. *)
            _matches.Height <- bottom_point_.Y - top_point_.Y

(* Constructor. *)
    do
(* Add event handlers. *)
        _win.Activated.Add activated_handler
        _win.Closing.Add closing_handler
        _win.IsKeyboardFocusWithinChanged.Add is_keyboard_focus_within_changed_handler
        _matches.commit.Add commit_handler
        _matches.cancel.Add cancel_handler
        _matches.general_error.Add _general_error.Trigger

(* Set properties. *)
(* Size the window to the combo box it contains. *)
        _win.SizeToContent <- SizeToContent.WidthAndHeight
(* Do not let the user resize the window. *)
        _win.ResizeMode <- ResizeMode.NoResize
(* The window should have no title bar or border. *)
        _win.WindowStyle <- WindowStyle.None
(* The window should always be shown in front of the editor. *)
        _win.Topmost <- true
(* Add the combo box to the window. *)
        _win.Content <- _matches
(* It seems calling this has no effect until the combo box has been added to the window. *)
        _matches.ApplyTemplate () |> ignore

(* Methods. *)

(* The Editor calls this method to update the position of the tag completion window after it is scrolled or resized. *)
/// <summary>Update the position of the tag completion window. (1) The top and left point at which to show the window. (2) The bottom point at which to show the window. Return unit.</summary>
    member this.Update top_point bottom_point =
(* If the tag completion window is visible... *)
        if _win.IsActive then
            do
(* Currently, this isn't needed. Originally we added it because the combo box drop down list did not always capture the mouse. That turned out to be a bug that we fixed. Before we fixed it, the user could scroll the editor and the drop down would become separated from the combo box. That's probably because the drop down is an instance of class System.Windows.Controls.Primitives.Popup, which appears to be a separate window. Currently, the user cannot scroll the editor or resize the application without closing the combo box drop down list. *)
(* Hide the combo box drop down list. *)
//                _matches.IsDropDownOpen <- false
(* Draw the window at the new location. *)
                draw_window top_point bottom_point

(* TODO2 Is there any way we can encapsulate more of the code required to use the TCCB?
- Textbox property - Does ComboBox have a property that normally returns the textbox, that we can override?
- ApplyTemplate
*)

/// <summary>Show the tag completion window. (1) The existing tags. (2) The initial tag value, or None. (2) The top and left point at which to show the window. (3) The bottom point at which to show the window. Return unit.</summary>
    member this.Show tags tag top_point bottom_point =
        do
(* Position the window. *)
            draw_window top_point bottom_point
(* Add the tags to the combo box. *)
            _matches.base_items <- tags |> List.map (fun tag -> tag.ToString ())
(* Show the window. We do not use ShowDialog because we want the user to be able to dismiss the window by clicking elsewhere. *)
            _win.Show () |> ignore
// TODO2 We believe this is never true.
(* If we have an initial tag value, add it to the combo box. *)
        match tag with
        | Some tag_ -> _matches.base_text <- tag_.ToString ()
        | None -> ()

/// <summary>Hide the tag completion window. Return unit.</summary>
    member this.Hide () = if _win.IsActive then do cancel_handler ()

(* Properties. *)

(* Events. *)

/// <summary>The user has finished entering a tag or has selected a tag. (1) The tag the user entered or selected. (2) Additional text the user entered, if any.</summary>
    member this.commit = _commit.Publish
/// <summary>A general non-critical error occurred. (1) The error message.</summary>
    member this.general_error = _general_error.Publish