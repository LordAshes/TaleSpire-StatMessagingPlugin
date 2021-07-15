# Stat Messaging Plugin

This unofficial TaleSpire plugin is for adding JSON broadcast messages to all clients.
If you plan to use the Creature Name synchronization for distributing messages to other client,
please use this plugin because it will ensure that there is no conflict between other plugins
using the same synchronization technique.

## Change Log

1.6.0: Addressed fix for TS Build 7035408

1.5.0: Modified to queue based solution so that processing is done on the main thread

1.4.1: Fixed character prevention bug

1.4.0: Added mini reset which allows a mini's Stat Messages to be reset if the messages are corrupt

1.4.0: Added diagnostic dump which writes the current selected mini's Stat Messages to the console

1.3.0: Added disgnostic mode toggle which displays Stat Messaging content at the top of the screen

1.2.1: Fixed bug with renaming mini

1.2.0: Implemented subscription implementation fixing the bug that one parent plugin consumed all changes.
       Plugins should now use the Subscribe() method and don't need to call the Check() method anymore but
	   legacy support has been added to support the previous Check() architecture while fixing the bug.
	   
1.2.0: Removed a log of diagnostic infomation put into the log

1.1.2: Bug fix to ReadInfo

1.1.1: Added ReadInfo to be able to read the last value for a given key

1.1.0: Initial contents are processed as changes

1.1.0: Reset added for situations like board changes where old data should be purged

1.0.0: Initial release

## Install

Install using R2ModMan or similar. The plugin is then available for any parent plugin to use it.

## Usage

To check for Stat Messaging changes (i.e. messages), issue the following command:

```C#
StatMessaging.Subscribe(*key*, *callback*);
```

This is a static method so you don't need to initialize any class to do it. This method triggers
the specified callback when the specified key changes. Multiple subscriptions to the same key can
be made (typically by different plugins) without one consuming the data changes. An asteriks (*) can be
used in place of the key to subscribe to all messages but this is typically not needed since a plugin
will be written for specific key changes. Subscribing to specific keys means the parent plugin will
only get changes for that key which typically means only changes that the plugin is interested in.
Using the wild card asteriks means the parent plugin gets all messages and needs to sort through them
in order to determine which are relevant. The subscribe method returns a Guid which can be used with
the Unscubscribe command to remove subscriptions. 

Messages are sent using a key/value pair system. Typically a plugin will set one or more keys and
will monitor for those key changes. To set a key with a value, issue the following command:

```C#
StatMessaging.SetInfo(cid, *keyName*, *content*);
```

Where keyName is a unique string that identifies the content. The keyName can be considered to
identify the communication channel (not the piece of data). Typically a plugin will use one key
for its information but in some cases a plugin may more than one key.

Where content is a string of the data to be sent.

It should be noted that Stat Messaging works on changes. Sending the same content as was already
posted will not generate new notifications. If it is possible that the same content needs to be
send multiple times, the plugin will need to implement a reset (e.g. change content to blank and
have the plugin ignore blank changes) and then repost the desired content.

To clear a key that is no loner needed, use the following code:

```C#
StatMessaging.ClearInfo(cid, *keyName*);
```

## Warning About Keys

Ideally a plugin should use one common key (i.e. a hard code key which is not determined by any
value such a campaign id or board id) for all communication. However, as mentioned, in some cases
using multiple keys can be advantageous. In such a case, please ensure that the number of keys
used is finite and small. There may be a limit to the Character Name length and using multiple
keys quickly depleats such a length limitation.

Similarly don't keys which are tied to some values that will change between sessions because
this will mean that the keys from old sessions will no longer be used but will still be present
on the minis.

if possible, use the ClearInfo() if a key is no longer going to be used.

## Rename Character Compensation

When a character is renamed using the GM Options, the trailing information (not viaible in the
rename) is likely to be erased. The Stat Messaging plugin has compensation for this and will
detect such occurances and re-link the last known stat block information with the renamed
character.

## Diagnostic Mode

When using Stat Messaging, you can toggle diagnostic mode on and off by pressing the coresponding
keyboard shortcut (default Left Control + Period). When diagnostic mode is on, the full name
and Stat Messaging keys will be displayed at the top of the screen. Diagnostic mode can be used
when testing parent plugin to verify that the Stat Messaging keys were properly updated.

## Diagnostic Dump

When using Stat Messaging, you can dump the current selected mini's Stat Messages (keys and values)
to the BepInEx console (and thus log)by using the coresponding keyboard shortcut (default
Left Control + Comma).

## Mini Reset

If a mini's Stat Messages become corrupt for some reason (usually a plugin using invalid character
with Stat Messaging) the mini's Stat Messages can be erased using the mini reset function triggered
manually by the coresponding keyboard shortcut (default Left Control + R).
