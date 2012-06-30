using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

/*
 * MIDIRead - A MIDI file analysis library for C#

    Written in 2012 by Vincent de Zwaan

    To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty. 

    You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>. 
  
 * Any modifications I make to this software in the future will likely be published at <https://github.com/VDZx/MIDIRead>.
 */

namespace MIDIRead
{
    public enum Format
    {
        SingleTrack,
        MultiTrack,
        MultiSong
    }

    public enum EventType
    {
        Sequence,
        Meta,
        Midi,
        Sysex
    }

    public enum MetaType
    {
        SequenceNumber,
        Text,
        Copyright,
        TrackName,
        InstrumentName,
        Lyric,
        Marker,
        Prefix,
        EndOfTrack,
        Tempo,
        SMPTE,
        TimeSignature,
        KeySignature,
        Specific,
        Cue,
        TimingClock,
        StartSequence,
        ContinueSequence,
        StopSequence,
        none
    }

    public enum MidiType
    {
        NoteOff,
        NoteOn,
        KeyAfterTouch,
        ControlChange,
        PatchChange,
        ChannelAfterTouch,
        PitchWheelChange,
        none
    }

    public enum NoteType
    {
        C,
        CSharp,
        D,
        DSharp,
        E,
        F,
        FSharp,
        G,
        GSharp,
        A,
        ASharp,
        B
    }

    public enum LengthType
    {
        DoubleWhole,
        Whole,
        Half,
        Quarter,
        Eighth,
        Sixteenth,
        ThirtySecond,
        SixtyFourth,
        HundredTwentyEight,
        Unknown
    }

    public class MIDIException : Exception { }
    public class InvalidMIDIFileException : MIDIException { }
    public class UnreadableTrackException : MIDIException { }
    public class UnsupportedMIDIFileException : MIDIException { }
    public class UnrecognizedEventException : MIDIException { }

    public class MIDIObject
    {
        public struct Header
        {
            /// <summary>
            /// Amount of bytes in the header, not counting the 'MThd' opening bytes or the header size bytes
            /// </summary>
            public int length;

            /// <summary>
            /// Whether the MIDI file is single-track, multi-track or multi-song.
            /// Only multi-track is supported at this time.
            /// </summary>
            public Format format;

            /// <summary>
            /// The amount of tracks in the midi file.
            /// </summary>
            public int tracks;

            /// <summary>
            /// Number of delta-time ticks per quarter note.
            /// </summary>
            public int ticksPerQuarterNote;
        }

        public struct Track
        {
            /// <summary>
            /// The data of the track in bytes.
            /// </summary>
            public byte[] data;

            /// <summary>
            /// Length of the track's data in bytes.
            /// </summary>
            public int datalength;

            /// <summary>
            /// An array of all meta and midi events in the sequence.
            /// </summary>
            public Event[] events;

            /// <summary>
            /// The track name.
            /// </summary>
            public string name;

            /// <summary>
            /// The notes played on this track.
            /// </summary>
            public PlayedNote[] notes;

            /// <summary>
            /// The sequence number of the sequence.
            /// </summary>
            public int number;
        }

        public struct Event
        {
            /// <summary>
            /// The data of the event in bytes.
            /// </summary>
            public byte[] data;

            /// <summary>
            /// If event is a meta event, the type of meta-event. Otherwise set to 'none'
            /// </summary>
            public MetaType metaType;

            /// <summary>
            /// If the event is not a meta event, the MIDI event.
            /// </summary>
            public MIDIEvent midiEvent;

            /// <summary>
            /// The time relative to the last event at which the event plays.
            /// </summary>
            public int time; //MIDI specifications say it should be 4 bytes at most, so not going to try to support more

            /// <summary>
            /// The type of the event.
            /// </summary>
            public EventType type;
        }

        public struct MIDIEvent
        {
            /// <summary>
            /// The channel the MIDI event applies to.
            /// </summary>
            public int channel;

            /// <summary>
            /// Used only for Control Change. The number of the controller to change.
            /// </summary>
            public int controllerNumber;

            /// <summary>
            /// The type of the MIDI event.
            /// </summary>
            public MidiType midiType;

            /// <summary>
            /// The note, if any, being played by the midi event.
            /// </summary>
            public Note note;

            /// <summary>
            /// Varies per MIDI event:
            /// Channel after-touch - Channel number
            /// Control Change - New value
            /// Patch Change - New patch
            /// Pitch Wheel Change - Amount of change (8192 is no change)
            /// </summary>
            public int value;
        }

        public struct Note
        {
            /// <summary>
            /// The MIDI note number of the note.
            /// </summary>
            public int number;

            /// <summary>
            /// The octave of the note.
            /// </summary>
            public int octave;

            /// <summary>
            /// The type of the note (C-C#-D-D#...A-A#-B).
            /// </summary>
            public NoteType type;

            /// <summary>
            /// The velocity at which the note is used.
            /// </summary>
            public int velocity;
        }

        public struct PlayedNote
        {
            /// <summary>
            /// For how long the note is played, in microseconds (millionth seconds).
            /// </summary>
            public long length;

            /// <summary>
            /// What kind of note it is (quarter-note, half-note, etc).
            /// Does not work properly yet.
            /// </summary>
            public LengthType lengthtype;

            /// <summary>
            /// Note object containing note type and velocity.
            /// </summary>
            public Note note;
            
            /// <summary>
            /// When the note is played, counted in microseconds (millionth seconds) from the start of the song.
            /// </summary>
            public long time;
        }

        public struct TempoChange
        {
            /// <summary>
            /// The tempo (in microseconds per quarter note) before the change.
            /// </summary>
            public long oldTempo;

            /// <summary>
            /// The tempo (in microseconds per quarter note) after the change.
            /// </summary>
            public long newTempo;

            /// <summary>
            /// The time in microseconds at which the tempo change takes place.
            /// </summary>
            public long time;
        }

        //-----Properties-----

        /// <summary>
        /// The data of the MIDI file in bytes.
        /// </summary>
        public byte[] data = null;

        /// <summary>
        /// Amount of bytes in the MIDI file
        /// </summary>
        public int filelength = 0;

        /// <summary>
        /// The MIDI file header
        /// </summary>
        public Header header;

        /// <summary>
        /// Whether or not the file has been loaded properly yet.
        /// </summary>
        public bool loaded = false;

        /// <summary>
        /// Set this to true to halt at any error, false to try to ignore errors. Default is true.
        /// </summary>
        private bool strict = true;

        /// <summary>
        /// The tempo the MIDI file starts with.
        /// </summary>
        public long tempo = 500000;

        /// <summary>
        /// The changes in tempo over the course of the MIDI.
        /// </summary>
        public TempoChange[] tempoChanges;

        /// <summary>
        /// The tracks in the MIDI file.
        /// </summary>
        public Track[] tracks;

        //-----Main Methods-----

        public MIDIObject(string midifile)
        {
            //this.strict = true;
            loaded = false;
            FileStream midifilestream = new FileStream(midifile, FileMode.Open);
            BinaryReader br = new BinaryReader(midifilestream);
            filelength = Convert.ToInt32(midifilestream.Length);
            data = br.ReadBytes(filelength);
            LoadMIDI(data);
            br.Close();
            midifilestream.Close();
        }

        /*public MIDIObject(string midifile, bool strict)
            : this(midifile)
        {
            this.trulystrict = strict;
        }*/

        public MIDIObject(byte[] data)
        {
            //this.strict = true;
            loaded = false;
            this.data = data;
            filelength = data.Length;
            LoadMIDI(data);
        }

        /*public MIDIObject(byte[] data, bool strict)
            : this(data)
        {
            this.trulystrict = strict;
        }*/

        private void LoadMIDI(byte[] data)
        {
            MemoryStream ms = new MemoryStream(data);
            BinaryReader br = new BinaryReader(ms);
            List<Track> tracks = new List<Track>();

            //=======Load header======
            //Confirm 'MThd'
            if (data[0] != 0x4D) throw new InvalidMIDIFileException();
            if (data[1] != 0x54) throw new InvalidMIDIFileException();
            if (data[2] != 0x68) throw new InvalidMIDIFileException();
            if (data[3] != 0x64) throw new InvalidMIDIFileException();
            br.ReadBytes(4); //Shift position 4 forward for rest of reading

            //Header length
            this.header.length = ReadInt(br);
            //Format
            int format = ReadShort(br);
            switch (format)
            {
                case 0: this.header.format = Format.SingleTrack; throw new UnsupportedMIDIFileException();
                case 1: this.header.format = Format.MultiTrack; break;
                case 2: this.header.format = Format.MultiSong; throw new UnsupportedMIDIFileException();
                default: throw new UnsupportedMIDIFileException();
            }
            //Tracks
            this.header.tracks = ReadShort(br);
            //Delta-time
            this.header.ticksPerQuarterNote = ReadShort(br);

            //======Load tracks=======
            for (int tracknumber = 0; tracknumber < this.header.tracks; tracknumber++)
            {
                Msg("Track number: " + tracknumber);
                Track track = new Track();
                //=======Load header=====
                byte[] trackheaderstart = br.ReadBytes(4);
                if (trackheaderstart[0] != 0x4D) throw new UnreadableTrackException();
                if (trackheaderstart[1] != 0x54) throw new UnreadableTrackException();
                if (trackheaderstart[2] != 0x72) throw new UnreadableTrackException();
                if (trackheaderstart[3] != 0x6B) throw new UnreadableTrackException();

                track.datalength = ReadInt(br);
                track.data = br.ReadBytes(track.datalength);

                //======Load track events======
                BinaryReader tbr = new BinaryReader(new MemoryStream(track.data));
                bool readingtrack = true;
                byte prevcommand = 0x00;
                List<Event> events = new List<Event>();
                while (readingtrack)
                {
                    Event ev = new Event();
                    ev.time = ReadVariable(tbr);
                    byte command = tbr.ReadByte();
                    Msg(ev.time + " Command " + command);
                    if (command >= 0x80)
                    {
                        if (command == 0xFF)
                        {
                            byte metacommand = tbr.ReadByte();
                            Msg("Meta: " + command);
                            ev.type = EventType.Meta;
                            ev.midiEvent = new MIDIEvent();
                            ev.midiEvent.midiType = MidiType.none;
                            switch (metacommand) //-------Check for meta-events
                            {
                                case 0x00: //Sequence number
                                    {
                                        ev.metaType = MetaType.SequenceNumber;
                                        int length = ReadByte(tbr);
                                        ev.data = tbr.ReadBytes(length);
                                        break;
                                    }
                                case 0x01: //Text event
                                    {
                                        ev.metaType = MetaType.Text;
                                        int length = ReadByte(tbr);
                                        ev.data = tbr.ReadBytes(length);
                                        break;
                                    }
                                case 0x02: //Copyright info
                                    {
                                        ev.metaType = MetaType.Text;
                                        int length = ReadByte(tbr);
                                        ev.data = tbr.ReadBytes(length);
                                        break;
                                    }
                                case 0x03: //Track name
                                    {
                                        ev.metaType = MetaType.TrackName;
                                        int length = ReadByte(tbr);
                                        ev.data = tbr.ReadBytes(length);
                                        track.name = Encoding.ASCII.GetString(ev.data);
                                        break;
                                    }
                                case 0x04: //Instrument name
                                    {
                                        ev.metaType = MetaType.InstrumentName;
                                        int length = ReadByte(tbr);
                                        ev.data = tbr.ReadBytes(length);
                                        break;
                                    }
                                case 0x05: //Lyric text
                                    {
                                        ev.metaType = MetaType.Lyric;
                                        int length = ReadByte(tbr);
                                        ev.data = tbr.ReadBytes(length);
                                        break;
                                    }
                                case 0x06: //Marker
                                    {
                                        ev.metaType = MetaType.Marker;
                                        int length = ReadByte(tbr);
                                        ev.data = tbr.ReadBytes(length);
                                        break;
                                    }
                                case 0x07: //Cue point
                                    {
                                        ev.metaType = MetaType.Cue;
                                        int length = ReadByte(tbr);
                                        ev.data = tbr.ReadBytes(length);
                                        break;
                                    }
                                case 0x2F: //End of track
                                    {
                                        ev.metaType = MetaType.EndOfTrack;
                                        readingtrack = false;
                                        ev.data = tbr.ReadBytes(1);
                                        break;
                                    }
                                case 0x51: //Set tempo - I don't get how this shit works
                                    {
                                        ev.metaType = MetaType.Tempo;
                                        int length = ReadByte(tbr);
                                        ev.data = tbr.ReadBytes(length);
                                        break;
                                    }
                                case 0x58: //Time signature
                                    {
                                        ev.metaType = MetaType.TimeSignature;
                                        int length = ReadByte(tbr);
                                        ev.data = tbr.ReadBytes(length);
                                        break;
                                    }
                                case 0x59: //Key signature
                                    {
                                        ev.metaType = MetaType.KeySignature;
                                        int length = ReadByte(tbr);
                                        ev.data = tbr.ReadBytes(length);
                                        break;
                                    }
                                case 0x7F: //Sequencer specific
                                    {
                                        ev.metaType = MetaType.Specific;
                                        int length = ReadByte(tbr);
                                        ev.data = tbr.ReadBytes(length);
                                        break;
                                    }
                                case 0xF8: //Timing clock
                                    {
                                        ev.metaType = MetaType.TimingClock;
                                        break;
                                    }
                                case 0xFA: //Start sequence
                                    {
                                        ev.metaType = MetaType.StartSequence;
                                        break;
                                    }
                                case 0xFB: //Continue sequence
                                    {
                                        ev.metaType = MetaType.ContinueSequence;
                                        break;
                                    }
                                case 0xFC: //Stop sequence
                                    {
                                        ev.metaType = MetaType.StopSequence;
                                        break;
                                    }
                                default: //----------Unrecognized event
                                    if (strict && metacommand != 0x21 && metacommand != 0x48) throw new UnrecognizedEventException();
                                    else
                                    {
                                        int length = ReadByte(tbr);
                                        tbr.ReadBytes(length);
                                    }
                                    break;
                            }
                        }
                        else //MIDI event
                        {
                            ev.type = EventType.Midi;
                            ev.metaType = MetaType.none;
                            MIDIEvent mev = new MIDIEvent();
                            mev.note = new Note();
                            string binary = Convert.ToString(command, 2);
                            Msg("Binary: " + binary);
                            while (binary.Length < 8) { binary = "0" + binary; }
                            mev.channel = Convert.ToInt32(Convert.ToUInt32(binary.Substring(4, 4), 2));
                            mev.controllerNumber = 0;
                            mev.note.number = 0;
                            mev.value = 0;

                            switch (binary.Substring(0, 4))
                            {
                                case "1000": //Note off
                                    mev.midiType = MidiType.NoteOff;
                                    mev.note.number = ReadByte(tbr);
                                    mev.note.velocity = ReadByte(tbr);
                                    break;
                                case "1001": //Note on
                                    mev.midiType = MidiType.NoteOn;
                                    mev.note.number = ReadByte(tbr);
                                    mev.note.velocity = ReadByte(tbr);
                                    break;
                                case "1010": //Key after-touch
                                    mev.midiType = MidiType.KeyAfterTouch;
                                    mev.note.number = ReadByte(tbr);
                                    mev.note.velocity = ReadByte(tbr);
                                    break;
                                case "1011": //Control change
                                    mev.midiType = MidiType.ControlChange;
                                    mev.controllerNumber = ReadByte(tbr);
                                    mev.value = ReadByte(tbr);
                                    break;
                                case "1100": //Patch change
                                    mev.midiType = MidiType.PatchChange;
                                    mev.value = ReadByte(tbr);
                                    break;
                                case "1101": //Channel after-touch
                                    mev.midiType = MidiType.ChannelAfterTouch;
                                    mev.value = ReadByte(tbr);
                                    break;
                                case "1110": //Pitch wheel change
                                    mev.midiType = MidiType.PitchWheelChange;
                                    mev.value = Convert.ToInt32(tbr.ReadUInt16()); //Yeah, this one is Little Endian. Great consistency.
                                    break;
                                case "1111": //Sysex event
                                    mev.midiType = MidiType.none;
                                    ev.type = EventType.Sysex;
                                    int length = ReadByte(tbr);
                                    ev.data = tbr.ReadBytes(length);
                                    break;
                                default:
                                    mev.midiType = MidiType.none;
                                    if (strict/* && command != 0x00*/) throw new UnrecognizedEventException();
                                    break;
                            }

                            //Fill in extra note information
                            if (mev.note.number != 0)
                            {
                                mev.note.octave = Convert.ToInt32(Math.Floor((double)mev.note.number / 12));
                                mev.note.type = (NoteType)(mev.note.number % 12);
                            }
                            else
                            {
                                mev.note.octave = 0;
                            }

                            ev.midiEvent = mev;
                        }
                        prevcommand = command;
                    }
                    //--------------------------------------------------------------------------------
                    else //Repeat MIDI command
                    {
                        byte firstdata = command;
                        command = prevcommand;
                        //Ugly copypasta from above - should be done better
                        ev.type = EventType.Midi;
                        ev.metaType = MetaType.none;
                        MIDIEvent mev = new MIDIEvent();
                        mev.note = new Note();
                        string binary = Convert.ToString(command, 2);
                        Msg("Binary: " + binary);
                        while (binary.Length < 8) { binary = "0" + binary; }
                        mev.channel = Convert.ToInt32(Convert.ToUInt32(binary.Substring(4, 4)));
                        mev.controllerNumber = 0;
                        mev.note.number = 0;
                        mev.value = 0;

                        switch (binary.Substring(0, 4))
                        {
                            case "1000": //Note off
                                mev.midiType = MidiType.NoteOff;
                                mev.note.number = Convert.ToInt32(Convert.ToUInt32(firstdata));
                                //mev.note.velocity = ReadByte(tbr);
                                mev.note.velocity = ReadByte(tbr);
                                break;
                            case "1001": //Note on
                                mev.midiType = MidiType.NoteOn;
                                mev.note.number = Convert.ToInt32(Convert.ToUInt32(firstdata));
                                mev.note.velocity = ReadByte(tbr);
                                break;
                            case "1010": //Key after-touch
                                mev.midiType = MidiType.KeyAfterTouch;
                                mev.note.number = Convert.ToInt32(Convert.ToUInt32(firstdata));
                                mev.note.velocity = ReadByte(tbr);
                                break;
                            case "1011": //Control change
                                mev.midiType = MidiType.ControlChange;
                                mev.controllerNumber = Convert.ToInt32(Convert.ToUInt32(firstdata));
                                mev.value = ReadByte(tbr);
                                break;
                            case "1100": //Patch change
                                mev.midiType = MidiType.PatchChange;
                                mev.value = Convert.ToInt32(Convert.ToUInt32(firstdata));
                                break;
                            case "1101": //Channel after-touch
                                mev.midiType = MidiType.ChannelAfterTouch;
                                mev.value = Convert.ToInt32(Convert.ToUInt32(firstdata));
                                break;
                            case "1110": //Pitch wheel change
                                mev.midiType = MidiType.PitchWheelChange;
                                //mev.value = Convert.ToInt32(tbr.ReadUInt16()); //Yeah, this one is Little Endian. Great consistency.
                                mev.value = Convert.ToInt32(tbr.ReadByte() * 256 + Convert.ToUInt32(firstdata));
                                break;
                            case "1111": //Sysex event
                                mev.midiType = MidiType.none;
                                ev.type = EventType.Sysex;
                                int length = Convert.ToInt32(Convert.ToUInt32(firstdata));
                                ev.data = tbr.ReadBytes(length);
                                break;
                            default:
                                mev.midiType = MidiType.none;
                                if (strict/* && command != 0x00*/) throw new UnrecognizedEventException();
                                break;
                        }

                        //Fill in extra note information
                        if (mev.note.number != 0)
                        {
                            mev.note.octave = Convert.ToInt32(Math.Floor((double)mev.note.number / 12));
                            mev.note.type = (NoteType)(mev.note.number % 12);
                        }
                        else
                        {
                            mev.note.octave = 0;
                        }

                        ev.midiEvent = mev;
                    }
                    //Add just specified event to list of events
                    events.Add(ev);
                }
                //Add just specified list of events to track
                track.events = events.ToArray();
                //Add just specified track to list of tracks
                tracks.Add(track);
                tbr.Close(); //Close the track binary reader
            }

            //Store the tracks
            this.tracks = tracks.ToArray();

            AnalyzeMIDI();

            loaded = true;
            br.Close(); //Close binaryreader
            ms.Close(); //Close memorystream
        }

        //======Generate other data from known information===
        private void AnalyzeMIDI()
        {
            long tempo = 500000; //Default tempo in case none is given

            //Create lists and arrays
            //-to put PlayedNotes in per track
            //-to store notes that have been pressed but not released
            //-To track what event we're checking, per track
            List<List<PlayedNote>> lists = new List<List<PlayedNote>>();
            List<List<PlayedNote>> unfinishednotes = new List<List<PlayedNote>>();
            for (int i = 0; i < tracks.Length; i++)
            {
                lists.Add(new List<PlayedNote>());
                unfinishednotes.Add(new List<PlayedNote>());
            }

            int[] currentevent = new int[lists.Count]; //Tracks the current event # per list
            long[] currenttime = new long[lists.Count];
            for (int i = 0; i < lists.Count; i++)
            {
                currentevent[i] = 0;
                currenttime[i] = 0;
            }

            List<TempoChange> tempoChanges = new List<TempoChange>();
            this.tempoChanges = new TempoChange[0];

            //Start reading all the lists at the same time, in MIDI timing order
            bool reading = true;

            while (reading)
            {
                //Check which next MIDI event of all tracks has the lowest delta time
                long lowesttime = long.MaxValue;
                int lowestlist = -1;
                for (int i = 0; i < lists.Count; i++)
                {
                    if (currentevent[i] < tracks[i].events.Length) //Ignore if at end of track
                    {
                        if (tracks[i].events[currentevent[i]].time + currenttime[i] < lowesttime)
                        {
                            lowestlist = i;
                            lowesttime = tracks[i].events[currentevent[i]].time + currenttime[i];
                        }
                    }
                }
                if (lowestlist == -1) { reading = false; } //All tracks at end of track
                else //You got the current event, parse it
                {
                    long newtempo = tempo;
                    Event ev = tracks[lowestlist].events[currentevent[lowestlist]];
                    if (ev.type == EventType.Midi)
                    {
                        ReanalyzeType: //Jumps here if case NoteOn realizes it's actually a NoteOff
                        switch(ev.midiEvent.midiType)
                        {
                            case MidiType.NoteOn: //Note on event
                                {
                                    if (ev.midiEvent.note.velocity == 0)
                                    {
                                        ev.midiEvent.midiType = MidiType.NoteOff;
                                        goto ReanalyzeType; //Return to start of switch to fall to NoteOff instead
                                    }
                                    PlayedNote pn = new PlayedNote();
                                    pn.note = ev.midiEvent.note;
                                    pn.time = currenttime[lowestlist] + TicksToMicroSeconds(ev.time, tempo, currenttime[lowestlist]);
                                    unfinishednotes[lowestlist].Add(pn);
                                    break;
                                }

                            case MidiType.NoteOff: //Note off event
                                {
                                    PlayedNote pn = new PlayedNote();
                                    for (int i = 0; i < unfinishednotes[lowestlist].Count; i++)
                                    {
                                        if (unfinishednotes[lowestlist][i].note.number == ev.midiEvent.note.number)
                                        {
                                            pn = unfinishednotes[lowestlist][i];
                                        }
                                    }
                                    unfinishednotes[lowestlist].Remove(pn); //TODO: Improve, causes 33% of CPU usage of entire LoadMIDI(), has to look for IndexOf() which causes slowdown
                                    pn.length = (currenttime[lowestlist] + TicksToMicroSeconds(ev.time, tempo, currenttime[lowestlist])) - pn.time;
                                    float division = pn.length / tempo;
                                    Msg("Division: " + division);
                                    if (division > 7.9 && division < 8.1) pn.lengthtype = LengthType.DoubleWhole;
                                    else if (division > 3.9 && division < 4.1) pn.lengthtype = LengthType.Whole;
                                    else if (division > 1.9 && division < 2.1) pn.lengthtype = LengthType.Half;
                                    else if (division > 0.9 && division < 1.1) pn.lengthtype = LengthType.Quarter;
                                    else if (division > 0.45 && division < 0.55) pn.lengthtype = LengthType.Eighth;
                                    else if (division > 0.20 && division < 0.30) pn.lengthtype = LengthType.Sixteenth;
                                    else if (division > 0.1125 && division < 0.1375) pn.lengthtype = LengthType.ThirtySecond;
                                    else if (division > 0.0620 && division < 0.0630) pn.lengthtype = LengthType.SixtyFourth;
                                    //Why hello there Beethoven!
                                    else if (division > 0.03120 && division < 0.03130) pn.lengthtype = LengthType.HundredTwentyEight;
                                    else pn.lengthtype = LengthType.Unknown;

                                    lists[lowestlist].Add(pn);
                                    break;
                                }
                        }
                    }
                    else if (ev.type == EventType.Meta)
                    {
                        switch (ev.metaType)
                        {
                            case MetaType.Tempo:
                                TempoChange tc = new TempoChange();
                                tc.time = currenttime[lowestlist] + TicksToMicroSeconds(ev.time, tempo, currenttime[lowestlist]);
                                tc.oldTempo = tempo;
                                newtempo = ConvertThreeByteInt(ev.data);
                                if (currenttime[lowestlist] == 0) this.tempo = newtempo;
                                tc.newTempo = newtempo;
                                
                                tempoChanges.Add(tc);
                                this.tempoChanges = tempoChanges.ToArray();
                                break;
                        }
                    }

                    currenttime[lowestlist] += TicksToMicroSeconds(ev.time, tempo, currenttime[lowestlist]);
                    tempo = newtempo;
                    currentevent[lowestlist]++;
                }
            }

            for (int i = 0; i < tracks.Length; i++)
            {
                tracks[i].notes = lists[i].ToArray();
            }
            this.tempoChanges = tempoChanges.ToArray();
        }

        //-----Support Methods---
        private int ReadInt(BinaryReader br) //Reads unsigned Big Endian 4-bit int and returns as int32
        {
            byte[] uf = br.ReadBytes(4);
            //uint fo = BitConverter.ToUInt32(new byte[] { uf[3], uf[2], uf[1], uf[0] }, 0);
            //return Convert.ToInt32(fo);
            return
                uf[0] * 16777216 +
                uf[1] * 65536 +
                uf[2] * 256 +
                uf[3];
        }

        private int ReadShort(BinaryReader br) //Reads unsigned Big Endian 2-bit int and returns as int32
        {
            byte[] uf = br.ReadBytes(2);
            return
                uf[0] * 256 +
                uf[1];
        }

        private int ReadByte(BinaryReader br) //Reads unsigned 1-bit int and returns as int32
        {
            return Convert.ToInt32(Convert.ToUInt32(br.ReadByte()));
        }

        //Disclaimer: This code was written around 5 AM. It might suck.
        private int ReadVariable(BinaryReader br) //Reads a MIDI variable length value
        {
            byte[] bytes = ReadVariableBytes(br);
            byte[] bytes2 = new byte[]{ 0, 0, 0, 0 };
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes2[bytes2.Length - 1 - i - (bytes2.Length - bytes.Length)] = bytes[i]; //TODO: Make this make sense
            }
            uint conv = BitConverter.ToUInt32(bytes2,0);
            return Convert.ToInt32(conv);
        }

        private byte[] ReadVariableBytes(BinaryReader br) //Reads a MIDI variable length value
        {
            //byte[] returnbytes;
            string binary = "";
            bool reading = true;
            while (reading)
            {
                string tempbin = Convert.ToString(br.ReadByte(), 2);
                while (tempbin.Length < 8) tempbin = "0" + tempbin;
                /*int start = 0;
                int length = tempbin.Length; if (length == 8) { length = 7; start = 1; }*/
                //binary = tempbin.Substring(1, 7) + binary;
                binary = binary + tempbin.Substring(1, 7);
                if (tempbin.Substring(0, 1) == "0") reading = false;
                else Msg("Repeated!");
                Msg(binary);
            }
            List<byte> bytes = new List<byte>();
            while (binary.Length % 8 != 0) { binary = "0" + binary; }
            for (int i = 0; i < binary.Length / 8; i++)
            {
                bytes.Add(Convert.ToByte(binary.Substring(i * 8, 8), 2));
            }
            return bytes.ToArray();
        }

        private long ConvertThreeByteInt(byte[] data)
        {
            return
                data[0] * 65536 +
                data[1] * 256 +
                data[2];
        }

        //Converts delta-time ticks (used for MIDI Event timing) to microseconds
        //Microseconds per tick = tempo / ticksPerQuarterNote
        public long TicksToMicroSeconds(long deltatime, long tempo, long trackLastEventTime)
        {
            long returnvalue = 0;
            long remainingDeltaTime = deltatime;

            if (tempoChanges.Length > 0)
            {
                TempoChange[] haxTempoChanges = new TempoChange[tempoChanges.Length + 1];
                for (int i = 0; i < tempoChanges.Length; i++) { haxTempoChanges[i] = tempoChanges[i]; }
                haxTempoChanges[haxTempoChanges.Length - 1] = new TempoChange()
                {
                    oldTempo = haxTempoChanges[haxTempoChanges.Length - 2].newTempo,
                    newTempo = haxTempoChanges[haxTempoChanges.Length - 2].newTempo,
                    time = long.MaxValue
                };

                long currentTime = trackLastEventTime;

                for (int i = 0; i < tempoChanges.Length - 1; i++)
                {
                    if (tempoChanges[i + 1].time <= currentTime)
                    {
                        //Skip this change; the next one starts before this is relevant
                    }
                    else
                    {
                        long endValue = tempoChanges[i + 1].time;

                        long timeToGrab = remainingDeltaTime * (tempoChanges[i].newTempo / header.ticksPerQuarterNote);
                        long deltaToRemove = remainingDeltaTime; //Take all by default
                        if (currentTime + timeToGrab > endValue)
                        {
                            //Whoa, you got too much, take a bit less
                            float percentageYouShouldGetInstead = (float)((float)endValue - (float)currentTime) / (float)timeToGrab;
                            deltaToRemove = Convert.ToInt64((float)deltaToRemove * (float)percentageYouShouldGetInstead);
                            timeToGrab = deltaToRemove * (tempoChanges[i].newTempo / header.ticksPerQuarterNote);
                        }

                        returnvalue += timeToGrab;
                        remainingDeltaTime -= deltaToRemove;
                    }
                }
            }

            returnvalue += remainingDeltaTime * (tempo / header.ticksPerQuarterNote);

            return returnvalue;
        }

        private void Msg(string msg)
        {
#if DEBUG
            System.Console.WriteLine(msg);
#endif
        }
    }
}
