
# KOKT Karaoke Party

Search for, queue up and play karaoke songs in the same unified app across YouTube, Karafun, and your local files!

Supports searching and playing:

- [Karafun](https://www.karafun.com/karaoke/) subscription (web player)
- YouTube (via [KaraokeNerds](https://karaokenerds.com/) search)
- Local playback from scanned folder (or drag-and-drop):
  - .mp4, .mkv, or .webm (via libvlc)
  - .cdg with adjacent .mp3
  - .zip of a .cdg and a .mp3

## What is this for?

This app is intended for "kiosk-style" karaoke parties.  

This means it is running on a machine with two displays (like a laptop with a TV or projector plugged in as a second monitor).  

It's designed to fully take over the second screen, using it to display the karaoke lyrics while a song is playing, or the "Next Up" or "queue is empty" screens between songs.  

The app's windows for searching and enqueueing songs appear on the main monitor (for example, the laptop's built-in screen, or wherever the keyboard and mouse are), and people come up and search for a song, double-click on it and type their singer name to add it to the queue.  

[<img src="docs/Concept%20Diagram.png" width="300"/>](docs/Concept%20Diagram.png)

You _could_ run karaoke for a room using this app by just doing those things yourself when people request songs from you…  
but to keep the app simple, it doesn't have any no singer/rotation management, you type in the singer name every time you queue something.  So you'd have to take care of that yourself as well.  (It's on the roadmap, but not a priority.)  
If you need singer management, but don't need the Karafun or YouTube playback and just want local song file playback, check out [OpenKJ](https://openkj.org/), which might be a better fit for you.

## How does it work?

When playing a local video or CDG/ZIP file, this app shows it in the same full-screen window it uses to show the Next Up or Empty Queue display.  

It now (Nov 2025) also works this way for YouTube playback, after first downloading the video to a temporary file via `yt-dlp`.  (If this fails, it will fall back to the following)

When playing off of Karafun (or YouTube after a failure to download), however, it hides that full-screen window and launches a browser it can automate, so that it can  
go to the web address for that song,  
click the full-screen button,  
click the play button (or the pause button if you pause the queue),  
watch for it to show signs of being finished playing,  
and return to the KOKT app.  
(You see this happen in real time, because there's no way for it to already _be_ full-screen, but it only takes a second and it's not that disruptive.)  

An advantage of this is that the browser can hang on to your login cookies for your Karafun account and YouTube Premium account (which you have logged into in that browser when you first set up the app), so it just works the same way as if you were visiting those things in your browser manually.  

(A disadvantage is that, at least so far, Karafun hasn't implemented any of the background-vocals text in their web player (nor in version 3 of their desktop app, which I'd guess is because they've unified their codebase around the web stuff?).  I don't know if they simply decided that was too difficult to implement,  or what, but I miss those extra words floating/flashing past the background, they were really fun for casual parties with many microphones, and if you already have all that extra timing data built up in your song library it feels bonkers to have it not do anything in your player anymore… I hope they smarten up and bring that back!)

## Why use this?

A Karafun subscription is a really good way to have a casual karaoke party with friends or coworkers or whatever,  
because you get easy, cheap access to many tens of thousands of songs,  
and they keep it updated with the new hits all the time.  

However, they don't have everything imaginable, especially if it's obscure, and if you do this regularly you might really want to sing something that just isn't available.  

Furthermore, like any commercial streaming library, Karafun's rights to songs have to be legally and monetarily maintained, so they are sometimes lost, and you're no longer able to sing those songs.  

Karafun Player 2 on the PC let you work around these problems by importing local files (assuming the files were named `{artist} - {title}`) into your library and including them in search results in the app,  And It Was Good.  

Unfortunately, the larger your local files library got, the more likely the app would just silently crash the next time you added any more to a watched folder, even if you'd already started singing.  
(Plus you'd lose the queue and history whenever this happened because it only saves those on _exit_, not as you go, for some reason.)  
It might do this a half dozen times in a row, after a couple minutes each time, before it ever managed to stay running again, and there was no way to know.  
So you started to feel reluctant to ever try to add any more songs, because you didn't want to spoil everyone's night by crashing the software over and over…  

Instead of fixing this, Karafun 3 just gets rid of the local files feature entirely.  
…Okay then.  

But any other karaoke apps can't access a Karafun subscription, so switching would require you to go ALL exclusively local files,  
which is a major effort (and, legally, cost-prohibitive!) to get the tens to hundreds of thousands of files all set up,  
and to continually update them with new things,  
not to mention just having the storage…  
Plus the apps out there just aren't easy-to-use for the casual karaoke party situation, as they're generally designed more for a dedicated karaoke DJ running it for a venue.  
Or, you could just do songs off YouTube (ideally somewhat vetting them via KaraokeNerds, because there's a lot of junk out there, but like, you do you I guess)...  
but then you can't build up any kind of queue while songs are being sung, you have to wait until one person is done singing and then look up the next one manually and there's dead air and all that…  

There had to be a better way.  

So I made one.  
😁

## Troubleshooting

### The display screen took over the same monitor as the app window so now I can't do anything!

You can press the Esc key to temporarily dismiss the display screen.  (It will pop back up again the next time something changes.)  
Then on the Setup tab in the app, use the set monitor number box and Apply button to change where it shows up.  
If display 0 is the only option, though, you probably have the displays mirrored (where you see the same thing on both screens), which won't work, you have to change that in your computer's settings so they're treated as two different screens.  (That, or you only have one display, in which case, this software is probably not for you, as mentioned above.  I guess you could keep using it by just pushing Esc every time though, if you like; who am I to tell you what to do?)

### But I don't have an Esc key!

Well that's not great. Are you on one of those macbooks that puts it on the bar, but the bar isn't working?  I think I heard Cmd+. might work as an Esc key in that situation, but I'm not sure.  
In Windows, if you can see the taskbar (on the other monitor?), you can use this trick to move a window that you can't see:  
hover the mouse over the taskbar item until it displays a little thumbnail of the window,  
then move the mouse onto the thumbnail and right-click it,  
then click "Move" on the little menu (if this is grayed out, click "Restore" first and then repeat these instructions),  
then push any arrow key once,  
and the outline of the window should now follow your mouse around until you left-click again.

### I can hear the music from a local .mp4 file but just see an empty purple-plasma window!

Yeah... Sometimes libvlc doesn't take over the display screen properly and spawns its own window instead, and I haven't figured out why yet (I can't reproduce it on my dev machine but it happens on the one I'm actually using the app regularly on).  
Press Esc to hide the display window, and you should see its video window on one of your screens; drag it to the right one if it isn't.  You should be able to rewind the song to the beginning using the progress bar at the top of the app.  When this has happened to me, it also didn't detect the song ending and I had to push the skip button to get the queue to advance.  Hopefully I'll get that figured out (or it just doesn't happen to anyone else ever, that would also be okay)  
_I'm not sure if this is still happening in Nov 2025 actually; I haven't seen it in a while, but I haven't been using local files much either... Will be doing so a lot more now that the YouTube is primarily `yt-dlp`-driven, so I guess we'll find out?_

### Something weird happened and I don't know what to do now

Close and reopen the software.  The queue is saved to disk so it should come back on just starting the current track over again (and the skip button should work if you don't want to do that).

## Some Major Roadmap Items (not necessarily in any order):

This section is now out of date as of Nov 2025, but I'll go through it at some point.

- Overall design/UX cleanup (I know, it's bad out there; out-of-the-box Godot user interface is not friendly)
- Cleaning up and opening source code
- Add a status bar (and use it to tell the user important things like that chromium is downloading, which right now you only get to know if you launch with console and are reading that, otherwise there's a big delay)
- Clean up the local files and scan results experience:
  - It doesn't reset the count after you close it, which is annoying
  - The last scan time doesn't update until you close and re-open the app
  - When you add a new path, it ought to prompt you to scan right away
  - A progress bar might be nice, if it can be made relatively accurate without slowing down the scan; we'd have to see
  - The formatting of information on the scanning dialog is a lazy mess
- Actually explain the filename metadata format specification, and add a try-it experience so you can see if it's correct before you commit to scanning
- Provide a way to clear out previously scanned stuff other than, like, deleting the sqlite database
- Find or write a proper datagrid instead of using Godot's tree to display results
  - That would allow sorting, for one thing, which is painful to be missing. Also resizing columns with the mouse like you'd expect to be able to do.
  - It could also open up the possibility of combining the search results into one big list (which I don't want to even entertain without sorting because some results are trash)
  - Also could display, as disabled, results from Karafun that are no longer licensed (which is a nicer user experience than just feeling gaslit when something that clearly should be there doesn't show up)
- Add configuration options for saving history
  - It just writes to a flat file per session right now
  - There's structure for it in the sqlite database but I haven't implemented writing to it yet
    - If I add a screen where you can actually see history, it'll read off of that
  - (I also plan to add saving history automatically to Azure Cosmos DB at some point, because I had a background service that would parse the karafun player 2 history and put it there, and that was nice for running stats)
- Be able to disable Karafun or KaraokeNerds or Local search results if you don't want to use them
- Maybe inject user scripts to hide page elements on YouTube and Karafun so that the loading isn't quite as jarring?
- Build the ability to search and queue items remotely (via phone browser or whatever)
- Build an optional singer/rotation management feature

## Need Help?

If you discover a bug (don't be shy, there are many right now I'm sure!), and it's not mentioned in the above couple sections, please [create a bug report](https://github.com/KaddaOK/KOKTKaraokeParty/issues).

Otherwise, for questions or comments, give me a shout in the [#kadda-ok](https://discord.com/channels/918644502128885760/1055115584007835668) channel on the diveBar discord.
  
## Contribute

Pull requests are welcome.  I also need people to test on other platforms, as I'm really only set up for Windows right now but cross-platform was the intent.
