[![stand with Ukraine](https://img.shields.io/badge/stand_with-ukraine-ffd700.svg?labelColor=0057b7)](https://stand-with-ukraine.pp.ua)
# MultiClip

## Overview

[![paypal](https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif)](https://oleg-shilo.github.io/cs-script/Donation.html)

MultiClip is not a unique clipboard manager. There are quite a few very good products exist in this category. Though MultiClip is a clipboard manager that puts an extremely strong emphasis on the user experience particularly tailored for the programmers.

MultiClip is noting else but an application that allows you to switch the clipboard content to any item from the interactive clipboard history dialog.  

These some of the strong MultiClip features:
- **Support for all clipboard formats**
  _The format of the clipboard content is indicated by the corresponding icon on the selection dialog_

- **auto-completion style user experience**
  _The popup selection dialog mimics auto-completion feature present in many IDEs._

- **System-wide hot-key**
  _The selection dialog can be invoked by pressing configurable hotkey combination regardless of the current application focus. You can also MultiClip as a generic application launcher by binding an additional hot-keys to the custom application._

- **Preview of the clipboard history content**
  _Non-obstructive immediate preview of the text and image content_

- **Indication of the history item "age"**
  _The left side colour bar indicates how old the item in the history list is._

- **Full history encryption**
  _The history is encrypted and stored in the user profile._  

- **Fully configurable**
  - Restoring the clipboard history after system restart
  - Start with Windows
  - Dark/Light theme
  
- **Zero-deployment**
  The whole product is a single file

## Installation

- Run _multiclip.exe_

- You can alsoinstall MultiClip form Chocolatey:
  ```
  choco install multiclip
  ```

---

### Important

Be aware MS _**Windows Defender**_ may identify multiclip as an application containing `Trojan:Script/Wacatac.B!ml` (or oter) virus. This is the case at least for for MultiClip v1.2.0-1.3.0.

While false positives are not so uncommon, it is rather puzzeling. This case the application that has passed multiple antivirus tests during Chocolayey moderation (virus screening and SHA protection) and yet, when deployed on the target PC it is flagged as dangerous by _Windows Defender_. 

_Windows Defender_ is in fact so aggressive that it deletes that executable even if you just build it in Visual Studio and start debugging it (with F5).
The executable not even have any tird-party dependency so the only possible trigger for this can be the use of low-level Win API for intercepting system wide hot-key combinations. 

You can address this problem by adding the Windows Defender exclusion for `C:\ProgramData\chocolatey\lib\Multiclip` folder (see [here](https://github.com/oleg-shilo/multiclip/raw/master/docs/defender_exclusion.png)). 

---

## Usage

- Press `` CTRL+` `` to popup clipboard history selection dialog

  ![](https://github.com/oleg-shilo/multiclip/blob/master/docs/selection.png)

- Select `settings` in the tray icon menu to popup the settings dialog:

![](https://github.com/oleg-shilo/multiclip/blob/master/docs/menu.png)

![](https://github.com/oleg-shilo/multiclip/blob/master/docs/config.png)

## Limitations

- Clipboard API is one of the oldest WIN32 API domain. While it works quite well it is suffering from a few nasty flaws.<br><br>
Thus one of the biggest surprises is the fact that despite all the exception handling you can possibly implement access to clipboard can trigger the native exceptions that is not handled properly by neither user code nor CLR itself. This can lead to the situation when the whole CLR goes down and kills the parent process. That's why Multiclip is implemented as a two process system where `multiclip.server.exe` ai constantly monitored by `multiclip.exe`, which restarts the server if it dies while accessing the clipboard content.<br><br>
This in turn can lead to the situation when occasionally Multiplip can miss and not record in the clipboard history a clipboard changes that triggered the failure. This problem has higher occurrence when triggered by highly diverse clipboard formats. Thus it has not been seen with the clipboard content that is a plain text only (e.g. copy text from Notepad).

- Some of the native MS application use Clipboard in a very exotic and way that affect other Clipboard clients (e.g. Multiclip). Thus Excel is the biggest offender. A single act of copying of a cell content can trigger multiple clipboard writing operations that are not transactional. Worth yet, some of the pure Excel clipboard formats are not even accessible from other processes. This can lead to the occasional reading (by Multiclip) failures that do not affect functionality and handled internally. But if the clipboard content cannot be read at all the Multiclip puts a string containing the location of the error log file so it can be used for troubleshooting:
```
"MultiClip Error: <log file path>" 
```

Thus you may see this item in the clipboard history list from time to time.
