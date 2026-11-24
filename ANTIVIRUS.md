# Antivirus warnings

A few antivirus engines flag FM27€0. These are false positives.

## What the warnings look like

On [VirusTotal](https://www.virustotal.com) usually one to three of about 70 engines flag it, with generic or reputation-based verdicts rather than a named threat. Typical ones:

| Verdict | What it means |
|---|---|
| McAfee "Ti!..." | Reputation: the file is new and rare |
| MaxSecure "...susgen" | Suspicious, generic |
| SecureAge "Malicious" | Flags most unsigned programs |

Every version is a new file, so it starts with no reputation again. You can upload any version to VirusTotal yourself to see its current result.

## Why it gets flagged

- **It edits another program's memory.** That is how every live editor works, FMRTE included: the editor opens fm.exe and reads and writes its memory to change players and clubs. Trainers and some malware do the same, so engines that judge by behaviour treat it as suspicious.
- **It is not code-signed.** A signing certificate costs money every year, which a free hobby project does not have.
- **It is new.** Few people have run it yet, so reputation-based engines have nothing to go on.

## What it does and does not do

- It only touches fm.exe, and only when you click **Attach & scan**.
- No network code. The only links are the Ko-fi and download page links, which open in your browser when you click them.
- Installs nothing, adds nothing to startup, does not change Windows settings.
- The editor keeps `settings.txt`, `changes.log`, and the `growth-preferences.xml` file and its write-lock file next to the exe.
- Not packed or obfuscated.

## Check it yourself

- The full source code comes with every release (`FM27E0-<version>-source.zip`).
- Run `build.bat` to build the exe yourself. It uses the C# compiler that ships with Windows, so nothing else needs to be installed.
- `notes.txt` in every release lists the SHA256 of the exe, so you can check that your download is the original.
