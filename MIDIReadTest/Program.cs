using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MIDIRead;

/*
 * MIDIRead - A MIDI file analysis library for C#

    Written in 2012 by Vincent de Zwaan

    To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty. 

    You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>. 
  
 * Any modifications I make to this software in the future will likely be published at <https://github.com/VDZx/MIDIRead>.
 */

namespace MIDIReadTest
{
    class Program
    {
        StreamWriter output;

        static void Main(string[] args)
        {
            new Program().DoStuff();
        }

        public void DoStuff()
        {
            Console.Write("Please enter a midi filename to analyze>");
            string filename = Console.ReadLine();
            if (!filename.Contains(".mid")) { Console.WriteLine("Is not a .mid file"); return; }
            MIDIObject mobj = new MIDIObject(filename);

            FileStream outputfile = new FileStream(filename.Replace(".mid", ".txt"), FileMode.OpenOrCreate);
            output = new StreamWriter(outputfile);

            Msg("MIDI FILE DATA:");
            Msg("");
            Msg("Header");
            Msg("======");
            Msg("Length: " + mobj.header.length);
            Msg("Format: " + mobj.header.format.ToString());
            Msg("Tracks: " + mobj.header.tracks);
            Msg("Delta-time: " + mobj.header.ticksPerQuarterNote);
            Msg("");
            for (int i = 0; i < mobj.tracks.Length; i++)
            {
                Msg("Track #" + i + " " + mobj.tracks[i].name);
                Msg("=========");
                for (int ii = 0; ii < mobj.tracks[i].events.Length; ii++)
                {
                    MIDIRead.MIDIObject.Event ev = mobj.tracks[i].events[ii];
                    Msg("Event " + ii + ": " + ev.type.ToString());
                    Msg("  Time: " + Convert.ToString(ev.time));
                    Msg("  Metatype: " + ev.metaType.ToString());
                    Msg("  Miditype: " + ev.midiEvent.midiType.ToString());
                    Msg("    Channel: " + ev.midiEvent.channel);
                    Msg("    Controller: " + ev.midiEvent.controllerNumber);
                    Msg("    Value: " + ev.midiEvent.value);
                    Msg("    Note: " + ev.midiEvent.note.number);
                    Msg("      Type: " + ev.midiEvent.note.type.ToString());
                    Msg("      Octave: " + ev.midiEvent.note.octave);
                    Msg("      Velocity: " + ev.midiEvent.note.velocity);
                    Msg("");
                }
                Msg("Notes:");
                Msg("------");
                for (int ii = 0; ii < mobj.tracks[i].notes.Length; ii++)
                {
                    MIDIRead.MIDIObject.PlayedNote pn = mobj.tracks[i].notes[ii];
                    Msg(pn.time + ": " + pn.note.number + " " + pn.note.type + pn.note.octave + " at " + pn.note.velocity +
                        " (" + pn.length + ", " + pn.lengthtype + ")");
                }
                Msg("");
            }
            Msg("Tempo changes");
            Msg("=============");
            long tempvalue = 0;
            for (int i = 0; i < mobj.tempoChanges.Length; i++)
            {
                Msg(mobj.tempoChanges[i].time + ": From " + mobj.tempoChanges[i].oldTempo + " to " + mobj.tempoChanges[i].newTempo);
                tempvalue += mobj.tempoChanges[i].time;
            }
            Msg("");
            Msg("END OF DATA");

            output.Close();
            outputfile.Close();
        }

        public void Msg(string msg)
        {
            output.WriteLine(msg);
        }
    }
}
