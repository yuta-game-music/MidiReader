
/**
  * To save the CSV file, uncomment the lines between [SAVE-CSV-BLOCK-START] and [SAVE-CSV-BLOCK-END]
  * CSV file name would be (base file name)_events.csv (for Events) and (base file name)_notes.csv (for Notes)
  */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MidiAnalyzer
{

    public class MidiAnalyzerCore
    {
        static short file_type = -1;
        public static int track_cnt { get; private set; } = 0;
        static int time_format = -1;
        static byte[] data;
        public static List<Note> note { get; private set; } = new List<Note>();
        public static List<Event> ev { get; private set; } = new List<Event>();
        static List<byte>[] track;
        static string FileName;

        public static void Main(string FileName)
        {
            Reset();
            MidiAnalyzerCore.FileName = FileName;
            System.IO.FileInfo file = new System.IO.FileInfo(FileName);
            data = new byte[file.Length];
            System.IO.FileStream fs = file.Open(System.IO.FileMode.Open);
            fs.Read(data, 0, (int)file.Length);
            fs.Close();
            Console.WriteLine("Finished reading");
            if (checkHeader())
            {
                Console.WriteLine("Finished loading. Converting the file...");
                Console.WriteLine("Finished Converting into events. Result code:" + readData());
                Console.WriteLine("Finished Converting into notes. Result code:" + getNotes());
                //[SAVE-CSV-BLOCK-START]
                //Console.WriteLine("Finished Creating event data. Result code:" + saveEventData());
                //Console.WriteLine("Finished Creating note data. Result code:" + saveNoteData());
                //[SAVE-CSV-BLOCK-END]
            }
            else
            {
                throw new Exception("The file might not be a MIDI file. Please check the file path.");
            }
        }

        static void Reset()
        {
            note.Clear();
            ev.Clear();
        }

        static Boolean checkHeader()
        {
            byte[] correct = new byte[8];
            correct[0] = 0x4D;
            correct[1] = 0x54;
            correct[2] = 0x68;
            correct[3] = 0x64;
            correct[4] = 0x00;
            correct[5] = 0x00;
            correct[6] = 0x00;
            correct[7] = 0x06;
            for (int i = 0; i < correct.Length; i++)
            {
                if (data[i] != correct[i]) { return false; }
            }
            file_type = (short)(data[9] + data[8] * 16);
            return true;
        }

        static int readData()
        {
            track_cnt = (int)DataExtractor.getDataRange(data, 10, 2);
            track = new List<byte>[track_cnt];
            Console.WriteLine("There are " + track_cnt + " track(s).");
            time_format = (int)DataExtractor.getDataRange(data, 12, 2);
            Console.WriteLine("Time format: " + time_format);
            //?????????????????????????????????????????????
            long n = 14;
            for (int i = 0; i < track_cnt; i++)
            {
                track[i] = new List<byte>();
                long checking_header = (long)DataExtractor.getDataRange(data, n, 4);
                if (checking_header != 0x4D54726B)
                {
                    throw new DataTypeErrorException("Error occurred when reading Track " + i);
                }
                n += 4;
                long track_length = (long)DataExtractor.getDataRange(data, n, 4);
                for (int j = 0; j < track_length; j++)
                {
                    track[i].Add(data[n + j + 4]);
                }
                n += track_length + 4;
            }
            //???????????????????????????????????????
            for (int tr = 0; tr < track_cnt; tr++)
            {
                long pos = 0;
                byte recent_ev_type = 0x80;
                byte this_ev_type = 0x80;
                while (track[tr].Count > 0)
                {
                    byte[] tmp_track = new byte[track[tr].Count];
                    track[tr].CopyTo(tmp_track);
                    long[] variable_length_num_output = DataExtractor.getVariableLengthNum(tmp_track.ToList<byte>());
                    pos += variable_length_num_output[0];
                    track[tr].RemoveRange(0, (int)variable_length_num_output[1]);
                    this_ev_type = track[tr][0];
                    if (this_ev_type < 0x80)
                    {
                        //??????????????????????????????
                        this_ev_type = recent_ev_type;
                    }
                    else
                    {
                        recent_ev_type = this_ev_type;
                        track[tr].RemoveAt(0);
                    }
                    if (this_ev_type == 0xF0)
                    {
                        //SysEX F0
                        variable_length_num_output = DataExtractor.getVariableLengthNum(track[tr]);
                        ev.Add(new Event(this_ev_type, pos, track[tr].GetRange(0, (int)variable_length_num_output[0]),tr));
                        track[tr].RemoveRange(0, (int)(variable_length_num_output[0] + variable_length_num_output[1]));
                    }
                    else if (this_ev_type == 0xF7)
                    {
                        //SysEX F7
                        variable_length_num_output = DataExtractor.getVariableLengthNum(track[tr]);
                        ev.Add(new Event(this_ev_type, pos, track[tr].GetRange(0, (int)variable_length_num_output[0]),tr));
                        track[tr].RemoveRange(0, (int)(variable_length_num_output[0] + variable_length_num_output[1]));
                    }
                    else if (this_ev_type == 0xFF)
                    {
                        //Meta Event
                        //ex??????????????????????????????????????????????????????(???????????????????????????)
                        variable_length_num_output = DataExtractor.getVariableLengthNum(track[tr].GetRange(1, track[tr].Count - 1));
                        List<byte> ex = new List<byte>();
                        ex.Add(track[tr][0]);
                        if (variable_length_num_output[0] > 0)
                        {
                            ex.AddRange(track[tr].GetRange(1 + (int)variable_length_num_output[1], (int)variable_length_num_output[0]));
                        }
                        ev.Add(new Event(this_ev_type, pos, ex, tr));
                        track[tr].RemoveRange(0, (int)(variable_length_num_output[0] + variable_length_num_output[1] + 1));
                    }
                    else
                    {
                        //MIDI Event
                        int ch = this_ev_type % 16;
                        this_ev_type -= (byte)ch;
                        if (this_ev_type == 0xC0 || this_ev_type == 0xD0)
                        {
                            ev.Add(new Event(this_ev_type, pos, track[tr].GetRange(0, 1), tr, ch));
                            track[tr].RemoveRange(0, 1);
                        }
                        else
                        {
                            if (this_ev_type == 0x90 && track[tr][1] == 0)
                            {
                                //????????????????????????0x90(???????????????)???????????????????????????0??????????????????????????????????????????????????????
                                //??????????????????????????????????????????????????????????????????????????????????????????????????????
                                ev.Add(new Event(0x80, pos, track[tr].GetRange(0, 2), tr, ch));
                            }
                            else
                            {
                                ev.Add(new Event(this_ev_type, pos, track[tr].GetRange(0, 2), tr, ch));
                            }
                            track[tr].RemoveRange(0, 2);
                        }
                    }
                }
            }
            return 0;
        }

        static int getNotes()
        {
            long[][] recent_note_on = new long[16][];
            long[][] recent_note_off = new long[16][];
            for (short i = 0; i < 16; i++)
            {
                recent_note_on[i] = new long[128];
                recent_note_off[i] = new long[128];
                for (int j = 0; j < 128; j++)
                {
                    recent_note_on[i][j] = -1;
                    recent_note_off[i][j] = -1;
                }
            }
            for (int i = 0; i < ev.Count; i++)
            {
                int ch = ev[i].ch;
                int note_no = ev[i].ex[0];
                if (ev[i].type == Event.EVENT_TYPE_NOTE_ON)
                {
                    if (recent_note_off[ch][note_no] != -1)
                    {
                        note.Add(new Note(ev[i], ev[(int)recent_note_off[ch][note_no]]));
                        recent_note_off[ch][note_no] = -1;
                    }
                    else if (recent_note_on[ch][note_no] == -1)
                    {
                        recent_note_on[ch][note_no] = i;
                    }
                    else
                    {
                        recent_note_on[ch][note_no] = i;
                        Console.WriteLine("Bad note-on detected.");
                    }
                }
                else if (ev[i].type == Event.EVENT_TYPE_NOTE_OFF)
                {
                    if (recent_note_on[ch][note_no] != -1)
                    {
                        note.Add(new Note(ev[(int)recent_note_on[ch][note_no]], ev[(int)i]));
                        recent_note_on[ch][note_no] = -1;
                    }
                    else if (recent_note_off[ch][note_no] == -1)
                    {
                        recent_note_off[ch][note_no] = i;
                    }
                    else
                    {
                        recent_note_off[ch][note_no] = i;
                        Console.WriteLine("Bad note-off detected.");
                    }
                }
            }

            return 0;
        }

        static string createEventData()
        {
            //????????????csv??????????????????????????????????????????(????????????????????????????????????????????????)(??????????????????????????????????????????)
            string output = "ch,pos,type\n";
            foreach (var j in ev)
            {
                /* 
                 * ??????_events.csv??????????????????????????????????????????
                 * ??????i??????????????????????????????????????????
                 * ?????????????????????????????????????????????????????????????????????
                 * j.ch : (?????????)??????????????????(????????????)-1
                 * j.pos : ??????
                 * j.type : ??????
                 * (??????????????????????????????????????????)*/
                output += j.ch + "," + j.pos + "," + j.type + "\n";
            }
            return output;
        }

        static int saveEventData()
        {
            System.IO.FileStream file = new System.IO.FileStream(FileName + "_events.csv", System.IO.FileMode.Create);
            byte[] data = Encoding.ASCII.GetBytes(createEventData());
            file.Write(data, 0, data.Length);
            return 0;
        }

        static string createNoteData()
        {
            //????????????csv??????????????????????????????????????????(????????????????????????????????????????????????)(??????????????????????????????????????????)
            string output = "ch,pos,dur,vel_on,vel_off,note_no\n";
            foreach (var i in note)
            {
                /* 
                 * ??????_notes.csv??????????????????????????????????????????
                 * ??????i??????????????????????????????????????????
                 * ?????????????????????????????????????????????????????????????????????
                 * i.ch : ???????????????
                 * i.note_on.pos ????????? i.pos : ????????????
                 * i.note_off.pos : ????????????
                 * i.dur ????????? i.getDuration() : ??????
                 * i.vel_on : ?????????????????????????????????
                 * i.vel_off : ?????????????????????????????????
                 * i.note_no : ?????????No.
                 * (??????????????????????????????????????????)*/
                output += i.ch + "," + i.pos + "," + i.getDuration() + "," + i.vel_on + "," + i.vel_off + "," + i.note_no + "\n";
            }
            return output;
        }

        static int saveNoteData()
        {
            System.IO.FileStream file = new System.IO.FileStream(FileName + "_notes.csv", System.IO.FileMode.Create);
            byte[] data = Encoding.ASCII.GetBytes(createNoteData());
            file.Write(data, 0, data.Length);
            return 0;
        }
    }

    public class Event
    {
        public static byte EVENT_TYPE_NOTE_OFF = 0x80;
        public static byte EVENT_TYPE_NOTE_ON = 0x90;
        public static byte EVENT_TYPE_POL = 0xA0;
        public static byte EVENT_TYPE_CC = 0xB0;
        public static byte EVENT_TYPE_PROGRAM = 0xC0;
        public static byte EVENT_TYPE_CH_PRESSURE = 0xD0;
        public static byte EVENT_TYPE_PITCH_WHEEL = 0xE0;
        public static byte EVENT_TYPE_SYSEX_0 = 0xF0;
        public static byte EVENT_TYPE_SYSEX_7 = 0xF7;
        public static byte EVENT_TYPE_META = 0xFF;

        public short type;
        public long pos;
        public int ch;
        public int track;
        public List<byte> ex = new List<byte>();

        public Event(short type, long pos, List<byte> ex, int track)
        {
            if (type < EVENT_TYPE_SYSEX_0) { Console.WriteLine("Error occurred when adding an event: this event type requires channel data."); }
            this.type = type;
            this.pos = pos;
            this.ex = ex;
            this.ch = -1;
        }
        public Event(short type, long pos, List<byte> ex, int track, int ch)
        {
            this.type = type;
            this.pos = pos;
            this.ex = ex;
            this.ch = ch;
        }
    }

    public class Note
    {
        public Event note_on;
        public Event note_off;
        public int note_no = -1;
        public long pos = -1;
        public int ch = -1;
        public long dur = -1;
        public int vel_on = -1;
        public int vel_off = -1;
        public int track = -1;

        public Note(Event note_on, Event note_off)
        {
            this.note_on = note_on;
            this.note_off = note_off;
            if (note_on.type != Event.EVENT_TYPE_NOTE_ON || note_off.type != Event.EVENT_TYPE_NOTE_OFF)
            {
                throw new InvalidEventsForNoteException("Error occurred when adding a note: invalid event(s) for note on/off");
            }
            else if (note_on.ch != note_off.ch)
            {
                throw new InvalidEventsForNoteException("Error occurred when adding a note: note on/off must be in the same channel.");
            }
            else if(note_on.track != note_off.track)
            {
                throw new InvalidEventsForNoteException("Error occurred when adding a note: note on/off must be in the same track.");
            }
            else if (note_on.ex[0] != note_off.ex[0])
            {
                throw new InvalidEventsForNoteException("Error occurred when adding a note: note on/off must be the same note No.");
            }
            this.ch = note_on.ch;
            this.pos = note_on.pos;
            this.dur = getDuration();
            this.vel_on = note_on.ex[1];
            this.vel_off = note_off.ex[1];
            this.note_no = note_on.ex[0];
            this.track = note_on.track;
        }

        int getNoteNo()
        {
            return note_on.ex[0];
        }

        public long getDuration()
        {
            return note_off.pos - note_on.pos;
        }
    }


    class DataExtractor


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MidiAnalyzer
{

    public class MidiAnalyzerCore
    {
        static short file_type = -1;
        public static int track_cnt { get; private set; } = 0;
        static int time_format = -1;
        static byte[] data;
        public static List<Note> note { get; private set; } = new List<Note>();
        public static List<Event> evs { get; private set; } = new List<Event>();
        static List<byte>[] track;
        //static string FileName;

        public static void Main(string FileName)
        {
            Reset();
            System.IO.FileInfo file = new System.IO.FileInfo(FileName);
            data = new byte[file.Length];
            System.IO.FileStream fs = file.Open(System.IO.FileMode.Open);
            fs.Read(data, 0, (int)file.Length);
            fs.Close();
            Debug.Log("Finished reading");
            if (checkHeader())
            {
                Debug.Log("Finished loading. Converting the file...");
                Debug.Log("Finished Converting into events. Result code:" + readData());
                Debug.Log("Finished Converting into notes. Result code:" + getNotes());
                //??????????????????????????????????????????????????????2???????????????????????????
                //Debug.Log("Finished Creating event data. Result code:" + saveEventData());
                //Debug.Log("Finished Creating note data. Result code:" + saveNoteData());
            }
            else
            {
                throw new Exception("The file might not be a MIDI file. Please check the file path.");
            }
        }

        static void Reset()
        {
            note.Clear();
            evs.Clear();
        }

        static Boolean checkHeader()
        {
            byte[] correct = new byte[8];
            correct[0] = 0x4D;
            correct[1] = 0x54;
            correct[2] = 0x68;
            correct[3] = 0x64;
            correct[4] = 0x00;
            correct[5] = 0x00;
            correct[6] = 0x00;
            correct[7] = 0x06;
            for (int i = 0; i < correct.Length; i++)
            {
                if (data[i] != correct[i]) { return false; }
            }
            file_type = (short)(data[9] + data[8] * 16);
            return true;
        }

        static int readData()
        {
            track_cnt = (int)DataExtractor.getDataRange(data, 10, 2);
            track = new List<byte>[track_cnt];
            Debug.Log("There are " + track_cnt + " track(s).");
            time_format = (int)DataExtractor.getDataRange(data, 12, 2);
            Debug.Log("Time format: " + time_format);
            //?????????????????????????????????????????????
            long n = 14;
            for (int i = 0; i < track_cnt; i++)
            {
                track[i] = new List<byte>();
                long checking_header = (long)DataExtractor.getDataRange(data, n, 4);
                if (checking_header != 0x4D54726B)
                {
                    throw new DataTypeErrorException("Error occurred when reading Track " + i);
                }
                n += 4;
                long track_length = (long)DataExtractor.getDataRange(data, n, 4);
                for (int j = 0; j < track_length; j++)
                {
                    track[i].Add(data[n + j + 4]);
                }
                n += track_length + 4;
            }
            //???????????????????????????????????????
            for (int tr = 0; tr < track_cnt; tr++)
            {
                long pos = 0;
                byte recent_ev_type = 0x80;
                byte this_ev_type = 0x80;
                while (track[tr].Count > 0)
                {
                    byte[] tmp_track = new byte[track[tr].Count];
                    track[tr].CopyTo(tmp_track);
                    long[] variable_length_num_output = DataExtractor.getVariableLengthNum(tmp_track.ToList<byte>());
                    pos += variable_length_num_output[0];
                    track[tr].RemoveRange(0, (int)variable_length_num_output[1]);
                    this_ev_type = track[tr][0];
                    if (this_ev_type < 0x80)
                    {
                        //??????????????????????????????
                        this_ev_type = recent_ev_type;
                    }
                    else
                    {
                        recent_ev_type = this_ev_type;
                        track[tr].RemoveAt(0);
                    }
                    if (this_ev_type == 0xF0)
                    {
                        //SysEX F0
                        variable_length_num_output = DataExtractor.getVariableLengthNum(track[tr]);
                        evs.Add(new Event(this_ev_type, pos, track[tr].GetRange(0, (int)variable_length_num_output[0]),tr));
                        track[tr].RemoveRange(0, (int)(variable_length_num_output[0] + variable_length_num_output[1]));
                    }
                    else if (this_ev_type == 0xF7)
                    {
                        //SysEX F7
                        variable_length_num_output = DataExtractor.getVariableLengthNum(track[tr]);
                        evs.Add(new Event(this_ev_type, pos, track[tr].GetRange(0, (int)variable_length_num_output[0]),tr));
                        track[tr].RemoveRange(0, (int)(variable_length_num_output[0] + variable_length_num_output[1]));
                    }
                    else if (this_ev_type == 0xFF)
                    {
                        //Meta Event
                        //ex??????????????????????????????????????????????????????(???????????????????????????)
                        variable_length_num_output = DataExtractor.getVariableLengthNum(track[tr].GetRange(1, track[tr].Count - 1));
                        List<byte> ex = new List<byte>();
                        ex.Add(track[tr][0]);
                        if (variable_length_num_output[0] > 0)
                        {
                            ex.AddRange(track[tr].GetRange(1 + (int)variable_length_num_output[1], (int)variable_length_num_output[0]));
                        }
                        evs.Add(new Event(this_ev_type, pos, ex, tr));
                        track[tr].RemoveRange(0, (int)(variable_length_num_output[0] + variable_length_num_output[1] + 1));
                    }
                    else
                    {
                        //MIDI Event
                        byte ch = (byte)(this_ev_type % 16);
                        this_ev_type -= (byte)ch;
                        if (this_ev_type == 0xC0 || this_ev_type == 0xD0)
                        {
                            evs.Add(new Event(this_ev_type, pos, track[tr].GetRange(0, 1), tr, ch));
                            track[tr].RemoveRange(0, 1);
                        }
                        else
                        {
                            if (this_ev_type == 0x90 && track[tr][1] == 0)
                            {
                                //????????????????????????0x90(???????????????)???????????????????????????0??????????????????????????????????????????????????????
                                //??????????????????????????????????????????????????????????????????????????????????????????????????????
                                evs.Add(new Event(0x80, pos, track[tr].GetRange(0, 2), tr, ch));
                            }
                            else
                            {
                                evs.Add(new Event(this_ev_type, pos, track[tr].GetRange(0, 2), tr, ch));
                            }
                            track[tr].RemoveRange(0, 2);
                        }
                    }
                }
            }
            evs.Sort((a, b) => { return (a.pos - b.pos>0)?1:((a.pos-b.pos<0)?-1:0); });
            float time = 0;
            long prev_pos = 0;
            long microsecFor4 = 500000;//bpm=120
            foreach(var ev in evs)
            {
                time += (ev.pos - prev_pos) * microsecFor4/1000000f/time_format;
                ev.time = time;
                prev_pos = ev.pos;
                if(ev.type==Event.EVENT_TYPE_META && ev.ex[0] == (int)Event.MetaType.Tempo)
                {
                    microsecFor4 = DataExtractor.getDataRange(ev.ex.ToArray(), 1, 3);
                    Debug.Log("Tempo set to "+(60.0*1000000/microsecFor4)+" at t="+time);
                }
            }
            return 0;
        }

        static int getNotes()
        {
            Dictionary<NoteEvent, Event> note_events = new Dictionary<NoteEvent, Event>(); 
            for (int i = 0; i < evs.Count; i++)
            {
                byte ch = evs[i].ch;
                int tr = evs[i].track;
                byte note_no = evs[i].ex[0];
                NoteEvent note_event = new NoteEvent(ch, tr, note_no);
                if (evs[i].type == Event.EVENT_TYPE_NOTE_ON)
                {
                    if (note_events.TryGetValue(note_event, out _))
                    {
                        Debug.Log("Bad note-on detected.");
                        note_events.Remove(note_event);
                    }
                    note_events.Add(note_event, evs[i]);
                }
                else if (evs[i].type == Event.EVENT_TYPE_NOTE_OFF)
                {
                    if (note_events.TryGetValue(note_event, out Event ev))
                    {
                        note.Add(new Note(ev, evs[i]));
                        note_events.Remove(note_event);
                    }
                    else
                    {
                        note_events.Remove(note_event);
                        Debug.Log("Bad note-off detected.");
                    }
                }
            }

            return 0;
        }

        static string createEventData()
        {
            //????????????csv??????????????????????????????????????????(????????????????????????????????????????????????)(??????????????????????????????????????????)
            string output = "ch,pos,type\n";
            foreach (var j in evs)
            {
                /* 
                 * ??????_events.csv??????????????????????????????????????????
                 * ??????i??????????????????????????????????????????
                 * ?????????????????????????????????????????????????????????????????????
                 * j.ch : (?????????)??????????????????(????????????)-1
                 * j.pos : ??????
                 * j.type : ??????
                 * (??????????????????????????????????????????)*/
                output += j.ch + "," + j.pos + "," + j.type + "\n";
            }
            return output;
        }

        /*static int saveEventData()
        {
            System.IO.FileStream file = new System.IO.FileStream(FileName + "_events.csv", System.IO.FileMode.Create);
            byte[] data = Encoding.ASCII.GetBytes(createEventData());
            file.Write(data, 0, data.Length);
            return 0;
        }*/

        static string createNoteData()
        {
            //????????????csv??????????????????????????????????????????(????????????????????????????????????????????????)(??????????????????????????????????????????)
            string output = "ch,pos,dur,vel_on,vel_off,note_no\n";
            foreach (var i in note)
            {
                /* 
                 * ??????_notes.csv??????????????????????????????????????????
                 * ??????i??????????????????????????????????????????
                 * ?????????????????????????????????????????????????????????????????????
                 * i.ch : ???????????????
                 * i.note_on.pos ????????? i.pos : ????????????
                 * i.note_off.pos : ????????????
                 * i.dur ????????? i.getDuration() : ??????
                 * i.vel_on : ?????????????????????????????????
                 * i.vel_off : ?????????????????????????????????
                 * i.note_no : ?????????No.
                 * (??????????????????????????????????????????)*/
                output += i.ch + "," + i.pos + "," + i.getDuration() + "," + i.vel_on + "," + i.vel_off + "," + i.note_no + "\n";
            }
            return output;
        }

        /*static int saveNoteData()
        {
            System.IO.FileStream file = new System.IO.FileStream(FileName + "_notes.csv", System.IO.FileMode.Create);
            byte[] data = Encoding.ASCII.GetBytes(createNoteData());
            file.Write(data, 0, data.Length);
            return 0;
        }*/
    }

    public class Event
    {
        public static byte EVENT_TYPE_NOTE_OFF = 0x80;
        public static byte EVENT_TYPE_NOTE_ON = 0x90;
        public static byte EVENT_TYPE_POL = 0xA0;
        public static byte EVENT_TYPE_CC = 0xB0;
        public static byte EVENT_TYPE_PROGRAM = 0xC0;
        public static byte EVENT_TYPE_CH_PRESSURE = 0xD0;
        public static byte EVENT_TYPE_PITCH_WHEEL = 0xE0;
        public static byte EVENT_TYPE_SYSEX_0 = 0xF0;
        public static byte EVENT_TYPE_SYSEX_7 = 0xF7;
        public static byte EVENT_TYPE_META = 0xFF;

        public enum MetaType
        {
            Tempo=0x51
        }

        public short type;
        public long pos;
        public byte ch;
        public int track;
        public List<byte> ex = new List<byte>();
        public float time;

        public Event(short type, long pos, List<byte> ex, int track)
        {
            if (type < EVENT_TYPE_SYSEX_0) { Debug.Log("Error occurred when adding an event: this event type requires channel data."); }
            this.type = type;
            this.pos = pos;
            this.ex = ex;
            this.ch = 255;
            this.track = track;
        }
        public Event(short type, long pos, List<byte> ex, int track, byte ch)
        {
            this.type = type;
            this.pos = pos;
            this.ex = ex;
            this.ch = ch;
            this.track = track;
        }
    }

    public class Note
    {
        public Event note_on;
        public Event note_off;
        public int note_no = -1;
        public long pos = -1;
        public int ch = -1;
        public long dur = -1;
        public int vel_on = -1;
        public int vel_off = -1;
        public int track = -1;

        public Note(Event note_on, Event note_off)
        {
            this.note_on = note_on;
            this.note_off = note_off;
            if (note_on.type != Event.EVENT_TYPE_NOTE_ON || note_off.type != Event.EVENT_TYPE_NOTE_OFF)
            {
                throw new InvalidEventsForNoteException("Error occurred when adding a note: invalid event(s) for note on/off");
            }
            else if (note_on.ch != note_off.ch)
            {
                throw new InvalidEventsForNoteException("Error occurred when adding a note: note on/off must be in the same channel.");
            }
            else if(note_on.track != note_off.track)
            {
                throw new InvalidEventsForNoteException("Error occurred when adding a note: note on/off must be in the same track.");
            }
            else if (note_on.ex[0] != note_off.ex[0])
            {
                throw new InvalidEventsForNoteException("Error occurred when adding a note: note on/off must be the same note No.");
            }
            this.ch = note_on.ch;
            this.pos = note_on.pos;
            this.dur = getDuration();
            this.vel_on = note_on.ex[1];
            this.vel_off = note_off.ex[1];
            this.note_no = note_on.ex[0];
            this.track = note_on.track;
        }

        int getNoteNo()
        {
            return note_on.ex[0];
        }

        public long getDuration()
        {
            return note_off.pos - note_on.pos;
        }
    }

    struct NoteEvent
    {
        byte ch;
        int tr;
        byte num;
        public NoteEvent(byte ch, int tr, byte num)
        {
            this.ch = ch;
            this.tr = tr;
            this.num = num;
        }
    }

    class DataExtractor
    {
        public static long getDataRange(byte[] data, long start_pos, long length)
        {
            long n = 0;
            for (long i = 0; i < length; i++)
            {
                n *= 256;
                n += data[start_pos + i];
            }
            return n;
        }
        public static long[] getVariableLengthNum(List<byte> data)
        {
            long num = 0;
            long cnt = 1;
            while (true)
            {
                if (data.Count == 0) { break; }
                byte i = data[0];
                if (i < 0x80)
                {
                    num = num * 128 + i;
                    break;
                }
                else
                {
                    num = num * 128 + (i - 0x80);
                    data.RemoveAt(0);
                    cnt++;
                }
            }
            return new long[2] { num, cnt };
        }
    }

    [System.Serializable]
    public class InvalidEventsForNoteException : Exception
    {
        public InvalidEventsForNoteException() { }
        public InvalidEventsForNoteException(string message) : base(message) { }
        public InvalidEventsForNoteException(string message, Exception inner) : base(message, inner) { }
        protected InvalidEventsForNoteException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class DataTypeErrorException : Exception
    {
        public DataTypeErrorException() { }
        public DataTypeErrorException(string message) : base(message) { }
        public DataTypeErrorException(string message, Exception inner) : base(message, inner) { }
        protected DataTypeErrorException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
