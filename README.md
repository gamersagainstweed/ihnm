
# ihnm (i have no microphone)
TTS overlay for games (Text-to-Speech). Glados voice. Soundboard that always shows your hotkeys in overlay. Unlimited switchable soundboards. Speech-to-Text-to-Speech. 40+ languages, custom voices supported. Synchronous multi-person playback of music in-game. Radio. Lipsync in SCP:SL that works with your mic input. Everything is local, no cloud.

## CUDA
Please install CUDA 11.8 (other versions aren't supported) for the best performance if you're using NVIDIA GPU. Kokoro voices and Speech-to-Text work bad without CUDA.

## Virtual Cable
This app won't work without virtual cable. I'm using this one: https://vb-audio.com/Cable/.

## Supported platforms.
For now it's Windows only but I'm gonna rewrite it from scratch and it'll be Linux-only.

## In-game usage
Please configure the game you want to use ihnm in to use borderless window mode.

# Overview

## Text-to-speech
ihnm allows you to talk in-game without microphone. There's a lot of pre-trained TTS models available for 40+ languages downloadable from the app.
ihnm can be invoked by pressing preconfigured key (Y by default).
After invoking it just type-in the sentence you want it to say and press enter.
To interrupt it mid-sentence invoke it and then press enter while sentence is being spelled.

### Voices selection
By default it uses glados but you can switch voices by typing-in following commands:

/v (voice) - to change to the specified voice

/v - to open GUI that lists all voices and allows filtering.

/voice command works the same way.

![image](https://github.com/user-attachments/assets/00032aaa-6175-447d-82df-a6034ecd4e44)

There's a lot of disinformation on reddit. Don't listen to these blatant liars, engimain.

### Custom voices
ihnm uses sherpa-onnx both for TTS and STT, therefore you can add custom vits and Kokoro voices.
To do so use sherpa-onnx guidelines for voice metadata conversion and then drop your voice into /sherpa/tts-models and create the corresponding txt file for it in /sherpaVoices/, add necessary info to it.

## Suggestions
In order to do autocomplete press 0-9 key corresponding to one under the suggestion you want to use or press one of these keys on your numpad instead.

If you press TAB the first suggestion will be used for the autocomplete. You can travel through the list of the suggestions using mousewheel or Up and Down arrows.

## Soundboard
If you want to play a sound you have to add it to ihnm first.
Please open the ihnm folder and then go to sounds/soundboard/ and drop your mp3 sounds there, other formats aren't supported.
You can organize them in subfolders as you wish. I recommend you to start each sound name with a special character to be able to easily find them from ihnm.
SFX from SCP:CB is included in default sounds that can be downloaded.

![image](https://github.com/user-attachments/assets/c89358ff-cb86-428e-9810-fc53a086dd3d)

You don't have to tie keys to sounds in ihnm and because of that you can have hunderds of sounds/music/songs.

Also you don't have to do some stupid rituals such as restarting the app at 4AM or counting on your fingers exclusively.

### Attaching hotkeys to sounds.
There's 2 ways.

1st one:
From ihnm use /bind command like that: /bind (sound) (hotkey). As you start entering the 2nd argument it will capture the keys you press instead of normal input which is cool.
It will create a hotkey in the currently active hotkeys page.

2nd one:
Please go to ihnm folder and then go to hotkeys/ and then to the folder that has the name of a number of a hotkeys page you want to place your hotkey in.
Create file (your sound name).txt without () in that folder, then open it and enter the keys that has to be pressed separated by '+'. You can find the keycodes here: https://sharphook.tolik.io/v5.3.9/articles/keycodes.html. Just use these without "Vc" prefix.

### Switching hotkeys pages from inside ihnm.
PageUp for the next hotkeys page, PageDown for the previous one.

Or using following command: /h (page number)

You can create new hotkeys using /h new (action) (hotkey) command as well.

/hotkey works the same way.

## Music
If you want to play custom music - better add it to /sounds/music/ folder, organize it into subfolders as you wish and start each music name with a special character as with sounds.
If you want to play music in sync with other people using ihnm use following command: 

/playmusicsync (music name) [ping]

Ping argument is for your current ping in the game you're playing. You can omit the ping argument and use /ping (ping) command instead before using /playmusicsync to specify your current ping.

## Songs
If you want to play custom songs you can just drop these into /sounds/music/ as well but if you want multiple voicetracks or lipsync in SCP:SL you should instead drop these
into /sounds/songs/. You have to create a separate folder for each song and name it after the song. Each song should contain 2 audio files: original.mp3 for voicetrack and instrumental.mp3 for instrumental track.
If you want to add other voicetracks you can name these as you wish.
To play a song just type-in the song name into the prompt or use /playsong command.
/playsong command offers several benefits.
By default ihnm will attempt to play the voicetrack that has the same name as the voice you're currently using, if it's not present it will play original.mp3

To specify a voicetrack you want it to use, use the following syntax: /playsong -(voicetrack) (song) 

To play only voice track add (-v) argument.
To play only instrumental track add (-i) argument.
If a voicetrack named after the voice you're currently using exists you can force ihnm to play original.mp3 by adding (-o) argument.

Just make sure that song name is the last argument.

### Playing songs in sync with other ppl who use ihnm.
Use /playsongsync command. It works the same way as /playsong command but also accepts ping argument which is a positive integer.
You can omit the ping argument and use /ping (ping) command instead before using /playsongsync to specify your current ping.

Just make sure that song name is the last argument.

It'll play it again after the song ends.

### Scatman
"I'm scatman" works the best with my clunky lipsync implementation so it has a dedicated command.

Use /scatman [ping] [-v] [-i] [-o] [-voicetrack] to play the song "I'm scatman" in sync with other people using ihnm.
It'll play it again after the song ends.

### Radio
There's a /radio command that works the same way as /scatman but plays 5 different songs in order included in default sounds that have to be downloaded first.
It's looped as well.

## Lipsync in SCP:SL
If enabled your character's lips movements in SCP:SL will correspond to sentences being spelled, songs being played and you mic input if enabled.
In order to configure SCP:SL to work with lipsync please run ihnm with lipsync enabled at least once and then restart the game after ihnm changes cmdbinds.txt file.

You can enable lipsync by using /lipsync on command even if it hasn't been turned on in the main window and disable it with /lipsync off.

## Aliases
Aliases allow you for example to type "mtf" instead of "mobile task force".
In order to create alias go to /aliases/ folder and create a .txt file with the name of the alias you want to create.
Inside the file write the contents of the alias (e.g. "mobile task force").
Aliases can contain multiple lines and execute commands. Commands always have to start from a newline.


You can create a recursive alias by calling the alias from within itself.

Example:

mtf.txt

`mobile task force mtf`

will play mobile task force until you interrupt it with invoke key + enter.

Don't do that:

mtf.txt

`mtf`

It'll quickly lead to stack overflow and crash - you have to keep it busy with smth so that you don't instantly fall into the abyss.

Example of using commands inside alias:

mtf.txt

`mobile`

`/pitch 0.9`

`task`

`/pitch 0.8`

`force`

`/pitch 1`

### Alias expansion
If alias consists of one line that's not a command the app will suggest you to expand the alias and you can proceed with it by pressing TAB.


## Adjusting pitch and tempo of voice
Use /pitch (pitch, 1 is default) to change the voice pitch.

Use /tempo (tempo, 1 is default) to change the voice tempo.

## Mic input
You can enable mic input so your voice can be heard on setup screen. Lipsync will work with it.

Turning off mic from ihnm:

Use /mic off command to turn of the mic after initial setup have been already completed.

Use /mic on command to turn it back on later.

## Speech-to-text
ihnm can do Speech-to-Text-to-Speech

In order to do that you have to download an STT model first and silerovadv5 VAD model as well. A lot of languages are supported.

If you want it to read the sentence as soon as it recognizes it pls check the "Realtime" checkbox.

If you don't check it it'll just be adding recognized sentences to the prompt and you can easily correct these before spelling using the selected voice.

## Typing digits
To type-in digits hold LeftAlt.

## Holding VTT key (Q)
Use /holdvtt (on/off) command to change the behavior of your game's VTT key. No need to hold it while playing music anymore as ihnm will kindly hold it for you.

By default vtt key is set to "Q" (sorry GMODers).

Use /vtt (key) command to specify another key.

### What the hell is VTT?
I'm surprised you asked but VTT stands for Voice-to-Talk. 

## Some useful commands

/vol (volume) - change the volume of everything except your mic input if it's enabled.

/pvol (playback volume) - change the playback volume of everything.

/mvol (microphone volume) - change the volume of the mic input.

There's a command /numberone that's like /scatman but for "We're Number One"

# What exactly is ihnm?
ihnm is a concept of a constantly present overlay that is largerly being accessed using a terminal-like prompt and changes its behaviour based on its contents.

For example it might offer suggestions or highlight parts of the prompt in various colors, change what L,R arrow keys do. 

The overlay can include GUI elements as well, those are interactable only if it's in the active state it can be switched to with a hotkey.

It eliminates the need to navigate complex UI layouts and memorize thoundsands of hotkeys.





# Download
You should download ihnm-v1.0-win64-cuda.zip on the Releases page. 

If for some mysterious reasons it crashes (like in my case) please download ihnm-v1.0-win64-cpu-only.zip

Unpack it where you wish and run ihnm.Desktop.exe

# License
The license is GPL-3 because GPL-3 libs were used.


`printf("Goodbye, weed");`
