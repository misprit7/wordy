<h1 align="center">
  <br />
  Wordy
</h1>

<p align="center">
  A compiler for the powerful .docx programming language, Wordy
</p>

<br />

[If you're anything like me](https://xkcd.com/1567/), you've tried every editor under the sun. Maybe you started with heavily integrated ones like [vscode](https://github.com/microsoft/vscode) or [Eclipse](https://www.eclipse.org/downloads/) and gradually branched out to [vim](https://www.vim.org/), [emacs](https://www.gnu.org/software/emacs/) or (if you're a real programmer) [ed](https://linux.die.net/man/1/ed). Perhaps you started with one and have been steadfast in your unwavering certainty of its superiority. And of course the wide variety of choices available applies to the choice of language doubly so, with a huge number of options to choose from. Whatever the case, I think that all programmers can relate to a feeling of something missing; a feeling that somewhere out there, there is perfect combination of language and editor that will bring them true enlightenment without and of [the](https://stackoverflow.com/questions/1700081/why-is-128-128-false-but-127-127-is-true-when-comparing-integer-wrappers-in-ja) [quirks](https://stackoverflow.com/questions/2192547/where-is-the-c-auto-keyword-used) [that](https://stackoverflow.com/questions/70882092/make-1-2-true) [come](https://stackoverflow.com/questions/57456188/why-is-the-result-of-ba-a-a-tolowercase-banana/57456236#57456236) [along](https://github.com/denysdovhan/wtfjs) [with](https://stackoverflow.com/questions/4176328/undefined-behavior-and-sequence-points) [other](https://www.seebs.net/faqs/c-iaq.html) [setups](https://xkcd.com/327/). But somehow that combination never seems to arrive. 

Until now that is. 

Wordy is the final iteration of language and IDE design. It combines the effectiveness of Microsoft Word, a powerful document editing tool with over 30 years of industry usage, with the reliability of some random guy's compiler hobby project with literally no experience in compiler design. For too long the ``.docx`` extension has been associated with business people and writers. It is time to claim it as the extension of the *programmer*. 

# Setup

This project is written in C#, not because I particular like the language but because of the [Open XML](https://learn.microsoft.com/en-us/office/open-xml/open-xml-sdk) library as well as being kind of fitting for an over the top Microsoft parody project. 

## Linux

Install [dotnet](https://wiki.archlinux.org/title/.NET)  on whatever distro you're running. Run the project with

```bash
cd compiler
dotnet run
```

## Windows

Why in the world would you want to run this project on Windows? Can't help you there. 

