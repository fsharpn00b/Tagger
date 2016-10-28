I wrote Tagger to gain practice in F# and WPF. Also, I wanted an editing tool that combined various features of Scrivener, OneNote, and Notepad++. In particular, I wanted the following features.

- Auto-save open files at a configurable interval.

- Edit two files side by side.

- Organize files and folders in a project using a tree view.

- Right-click a line and move it to another file in the project.

- Maximize editing space. We do this by hiding the project pane and using context menus to control the application rather than drop-down menus.

- Auto-save the undo stack for a file when you close it, and restore it when you re-open it.

- Tag a line by entering a single character (such as '#') followed by a word. Right-clicking a tag should allow you to easily find other lines that have the same tag, throughout the project. Tags should also be in plain text, so files can be read and edited in other editors besides Tagger.

- Select an entire line at once by clicking in the margin at that line.

- Copy or move a line by clicking in the margin at one line and then dragging to another line, in the same file or another file.

- Select multiple lines that are not contiguous, and copy or move them to another place in the same file, or to another file.

This documentation is still a work in progress. If you have any issues please send mail to fsharpn00b@users.sourceforge.net.

*

Getting Started

NOTE Tagger is a Windows application and requires the .NET Framework 4.5, which you can get here.
http://www.microsoft.com/en-us/download/details.aspx?id=30653
Tagger is portable. It requires no installation process, and it creates no registry entries.

The Tagger application consists of three panes.
1. To the left is the project pane. You can click the expander arrow (< or >) to the left of this pane to show or hide it.
2. In the center is the left editing pane.
3. To the right is the right editing pane. You can click the expander arrow (< or >) to the right of this pane to show or hide it. When the right editing pane is shown, you can grab the vertical splitter between the two editing panes and drag it left or right to change the amount of screen space given to each of them. If you hide the right editing pane, it will remember the position of the splitter the next time you show it.

When you start Tagger, the project pane contains the text "No project loaded". Right-click on this text and a context menu appears with the following items.

- New Project. This opens the Browse for Folder window. Here you can browse for an existing folder, or create a new one, where your new project is to be saved. If the folder already contains files and subfolders, Tagger will load them into the new project. Tagger ignores files that do not match the file filter pattern specified in the configuration. For more infomation see the "File Filter Pattern" setting in Configuration.

- Open Project. This opens the Browser for Folder window. Here you can browse for a folder in which an existing project is saved. Tagger tries to find a project.ini file in this folder. A project.ini file contains information about the project, such as the order in which files are listed in the project pane, what tabs are currently open, and the vertical scroll position of each file. If Tagger cannot find a project.ini file, it loads the existing files and subfolders in the folder as if you were creating a new project.

- Open Configuration. This opens the Configuration window.

For now, select New Project and browse to an empty folder or create a new one. The project pane changes to show the name of the folder you selected.

Right-click on the folder name and select "New File". The New File dialog appears.

In the New File dialog, enter a name such as "test" and click OK. Tagger appends the ".txt" extension to the name automatically.

In the project pane, you can now expand the folder node to show the new file you created. Now either double-click the file name, or right-click on it and select "Open File in Left Pane" from the context menu. The file opens in the left editing pane. A tab appears above the editor to represent the file. A red line appears between the editor and the row of tabs to show that the editor has focus.

If you right-click on a line in the editor, a context menu appears with the following items.

- Move To. This lets you select a different file in the project. Tagger moves the line to the end of that file.

- Add Tags. This lets you add multiple tags to the line. For more infomation see Tagging.

- Word Count. This shows the word count for the file.

- Find in Project. This lets you enter a word to search for in all project files.

- Find All Tags in Project. This shows all tagged lines in all files in the project. For more infomation see Tagging.

If you right-click on a word, the Find in Project menu item lets you search for that word without typing it in.

If you select multiple words and right-click on the selection, the Find in Project menu item lists all of the words in the selection.

You can select an entire line by clicking in the margin at that line. The margin is a vertical gray area that appears to the left and right of each editor. When you select a line in the margin, a red highlight appears in the margin, and a dark blue background appears behind the text of that line. You can clear a margin selection by pressing Escape.

You can select multiple lines by clicking in the margin while pressing Shift or Control.

If you select multiple lines and then right-click on the selection, the Move To menu item causes Tagger to move all of the selected lines to the file you select.

If you right-click on a selection, the Word Count menu item shows the word count only for the selection, rather than the entire file.

If you right-click on a URL, an extra item appears in the context menu: Add On Commands. This item expands to another item: Open URL. If you have the Tagger add on for Firefox and it is connected to Tagger, this shows the Open URL dialog. This lets you open the URL in Firefox. If you select multiple URLs and right-click on the selection, the Add On Commands context menu contains an Open URL item for each of the URLs.

*

Tagging

Tagging helps you organize lines of text by adding plain-text tags to them. In the editor, enter the tag symbol (the default is "#", but you can change it in the configuration settings). The tag completion drop down appears. This contains a most-recently-used list of tags. It also auto-completes tags for you - press the Tab, Space, or Enter key to auto-complete a tag.

When you right-click on a tag, Tagger shows the Find in Project Results window, with all lines that contain the tag, in all files in the project. This lets you see related lines of text in a single place even though they are distributed across multiple files.

The Find in Project Results window contains two buttons at the lower right.

- Dump Results. This converts the find results to text and adds them to the file you are currently editing, at the cursor.

- Copy Results. This converts the find results to text and copies them to the clipboard.

*

Controls

Project Pane

You can drag and drop files and folders to change the order in which they appear. However, Tagger always lists folders before files.

If you right-click on a file, a context menu appears that lets you open that file in the left or right editing pane, or rename the file.

If you right-click on the project folder when a project is loaded, a context menu appears with the items described previously (New Project, Open Project, Open Configuration) plus the following items.

- Save Project. This saves information about the project, such as the order in which files are listed in the project pane, what tabs are currently open, and the vertical scroll position of each file. You do not need to worry about this, however, as Tagger does this automatically whenever there is a change to the project information.

- New File. This lets you create a file under the project folder.

- New Folder. This lets you create a folder under the project folder.

If you right-click on a folder, a context menu appears with the items New File and New Folder. These let you create files and folders below that folder.

Tabs

You can drag and drop tabs to change the order in which they appear.

If you right-click on a tag, a context menu appears that lets you close that tab, or all tabs.

*

Configuration

To configure Tagger, right-click on the project name in the project pane and select "Open Configuration" from the context menu. The settings are as follows.

- Mouse Hover Delay. This currently has no effect, as Tagger does not use tool tips.

- Mouse Wheel Scroll Speed. This controls how many lines the editor scrolls when you move the mouse wheel up or down one notch. The value must be at least 1.

- Drag Scroll Speed. This controls how quickly the editor scrolls when you drag the mouse near the top or bottom of the editor. The editor scrolls X number of lines per second, where X is the value. The value must be at least 1.

- Highlight Display Time. This controls how many milliseconds Tagger shows a highlight after you drag and drop one or more lines in the editor or select a Find in Project result. The value must be at least 1000 (1 second).

- Font Size. This controls the font size in the editor. The value must be at least 1.

- File Auto-Save Interval. This controls how often Tagger saves a file automatically while it is open in the editor. Note the file is saved only if it has changed. The value must be at least 1000 milliseconds (1 second).

- Project Auto-Save Interval. This currently has no effect. Tagger saves the project automatically whenever there is a change to any project information, such as when you open a tab or change the display order of the files in the project pane.

- Project Backup Folder. This controls the folder where Tagger saves a backup of the currently open project when you open a different project or close Tagger. The value must be a valid folder that Tagger can write to. The backup is a zip file that contains all files and folders in the project. The zip file name is ProjectName_YYYYMMDD_hhmmss. YYYYMMDD is the year, month, and day, and hhmmss is the hour, minute, and second when the backup was made. The hours range from 00 to 23, so AM/PM is not needed.

- Default New File Extension. This controls the file extension that Tagger adds automatically to any new file you create in Tagger. The value can be blank, in which case no extension is added to new files. If the value is not blank, it must be a period followed by one or more letters and numbers. If you create a new file in Tagger and include a file extension in the file name, Tagger ignores this for that file.

- File Filter Pattern. This controls what files Tagger shows in the project pane. The value can by empty. Otherwise, it must contain one or more valid regular expressions. The expressions must be separated by newlines. If a given file matches one or more of the expressions, Tagger shows it in the project pane.

- Recent Projects List. This is a list of folders that contain projects that you recently opened in Tagger. When you tell Tagger to open a project, the first folder in this list is selected automatically in the Browse For Folder window. You can edit this list, but each folder must be valid, and the folders must be separated by newlines.

