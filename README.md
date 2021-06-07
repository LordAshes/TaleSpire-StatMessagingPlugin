# Stat Messaging Plugin

This unofficial TaleSpire plugin is for adding JSON broadcast messages to all clients.
If you plan to use the Creature Name synchronization for distributing messages to other client,
please use this plugin because it will ensure that there is no conflict between other plugins
using the same synchronization technique.

## Change Log

1.0.0: Initial release

## Install

Install using R2ModMan or similar. The plugin is then available for any parent plugin to use it.

## Usage

To check for Stat Messaging changes (i.e. messages), issue the following command:

```C#
StatMessaging.Check(requestHandler.Request);
```

This is a static method so you don't need to initialize any class to do it. Normally this check is
placed in the *Update()* method so that changes are detected each update cycle. The method has its
own flow control so that if a new check is done before the previous one finished it will skip the
check (thus preventing a potential overflow).

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
