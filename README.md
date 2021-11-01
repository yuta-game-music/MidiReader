# MidiReader
Midi file reader (no built file)

# Usage
All functions are static
1. Download MidiAnalyzer.cs and import it into your project
2. Call MidiAnalyzer.Main(string FileName)
3. You can now fetch events and notes

If you want to read another file, repeat 1-2

# Known issues
- throws exceptions or messages when MIDI file has notes which are on the same channel, at the same time, but on different tracks
  - ex: Note A (channel=1,track=1,pos=4-8) and Note B (channel=1,track=1,pos=6-10) will cause "Bad note-on" error, then "Bad note-off" error
  - ex: Note A (channel=1,track=1,pos=4-8) and Note B (channel=1,track=**2**,pos=6-10) will throw InvalidEventsForNoteException
