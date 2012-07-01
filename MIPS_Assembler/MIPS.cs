using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public struct Instruction
{
    public int binary;
    public char format;

    public Instruction(int f)//r instruction
    {
        binary = f;
        format = 'R';
    }

    public Instruction(int o, char t)//i or j instruction
    {
        binary = o << 26;
        format = t;
    }
}

class MIPS
{
    const int DEFAULT_START = 1000;

    static List<int> addresses = new List<int>();
    static List<string> input = new List<string>();
    static List<string> binary = new List<string>();
    static List<string> hex = new List<string>();

    static List<string> rBasic = new List<string> { "xor", "sra", "sllv", "srlv", "srav", "add", "sub", "and", "or", "nor", "slt" };
    static List<string> iBasic = new List<string> { "xori", "addi", "subi", "andi", "ori" };

    static Dictionary<string, int> registers = new Dictionary<string, int>
    {
        {"$zero", 0},
        {"$at", 1},
        {"$v0", 2}, {"$v1", 3},
        {"$a0", 4}, {"$a1", 5}, {"$a2", 6}, {"$a3", 7},
        {"$t0", 8}, {"$t1", 9}, {"$t2",10}, {"$t3",11}, {"$t4",12}, {"$t5",13}, {"$t6",14}, {"$t7",15},
        {"$s0", 16}, {"$s1", 17}, {"$s2", 18}, {"$s3", 19}, {"$s4", 20}, {"$s5", 21}, {"$s6", 22}, {"$s7", 23},
        {"$t8", 24}, {"$t9", 25},
        {"$k0", 26}, {"$k1", 27},
        {"$gp", 28},
        {"$sp", 29},
        {"$fp", 30},
        {"$ra", 31}
    };

    static Dictionary<string, Instruction> instructions = new Dictionary<string, Instruction>();
    
    //number of extra lines from translation
    static Dictionary<string, int> pseudos = new Dictionary<string, int>
    {
        {"blt", 1}, {"bgt", 1}, {"ble", 1}, {"move", 0}, {"mul", 1}, {"rem", 1}, {"li", 1}, {"la", 1}, {"b", 0}, {"clear", 0}, {"bge", 1}
    };

    static Dictionary<string, int> labels = new Dictionary<string, int>();

    static Dictionary<char, int> hexToDecValues = new Dictionary<char, int>();

    static void fillDictionaries()
    {
        //in order of page 281
        instructions.Add("xor", new Instruction(38));
        instructions.Add("xori", new Instruction(14, 'I'));
        instructions.Add("sra", new Instruction(3));
        instructions.Add("sllv", new Instruction(4));
        instructions.Add("srlv", new Instruction(6));
        instructions.Add("srav", new Instruction(7));
        instructions.Add("mthi", new Instruction(17));
        instructions.Add("mtlo", new Instruction(19));
        instructions.Add("lh", new Instruction(33, 'I'));
        instructions.Add("lb", new Instruction(32, 'I'));
        instructions.Add("lwl", new Instruction(34, 'I'));
        instructions.Add("lwr", new Instruction(38, 'I'));
        instructions.Add("swl", new Instruction(42, 'I'));
        instructions.Add("swr", new Instruction(46, 'I'));
        instructions.Add("ll", new Instruction(48, 'I'));
        instructions.Add("sc", new Instruction(56, 'I'));
        instructions.Add("movz", new Instruction(10));
        instructions.Add("movn", new Instruction(11));
        instructions.Add("jalr", new Instruction(9));
        instructions.Add("syscall", new Instruction(12, 'I'));
        instructions.Add("break", new Instruction(13, 'I'));

        //in order of lab sheet with duplicates removed
        instructions.Add("add", new Instruction(32));
        instructions.Add("addi", new Instruction(8, 'I'));
        instructions.Add("sub", new Instruction(34));
        instructions.Add("and", new Instruction(36));
        instructions.Add("andi", new Instruction(12, 'I'));
        instructions.Add("or", new Instruction(37));
        instructions.Add("ori", new Instruction(13, 'I'));
        instructions.Add("nor", new Instruction(39));
        instructions.Add("sll", new Instruction(0));
        instructions.Add("srl", new Instruction(2));
        instructions.Add("lw", new Instruction(35, 'I'));
        instructions.Add("lui", new Instruction(15, 'I'));
        instructions.Add("sw", new Instruction(43, 'I'));
        //d.Add("lb", new Instruction(32, 'i'));
        instructions.Add("sb", new Instruction(40, 'I'));
        instructions.Add("beq", new Instruction(4, 'I'));
        instructions.Add("bne", new Instruction(5, 'I'));
        instructions.Add("slt", new Instruction(42));
        instructions.Add("j", new Instruction(2, 'J'));
        instructions.Add("jr", new Instruction(8));
        instructions.Add("jal", new Instruction(3, 'J'));

        hexToDecValues.Add('0', 0);
        hexToDecValues.Add('1', 1);
        hexToDecValues.Add('2', 2);
        hexToDecValues.Add('3', 3);
        hexToDecValues.Add('4', 4);
        hexToDecValues.Add('5', 5);
        hexToDecValues.Add('6', 6);
        hexToDecValues.Add('7', 7);
        hexToDecValues.Add('8', 8);
        hexToDecValues.Add('9', 9);
        hexToDecValues.Add('A', 10);
        hexToDecValues.Add('B', 11);
        hexToDecValues.Add('C', 12);
        hexToDecValues.Add('D', 13);
        hexToDecValues.Add('E', 14);
        hexToDecValues.Add('F', 15);
    }

    //convert 2-bit hex to 8-bit binary
    static string hexToBinary(string h)
    {
        return Convert.ToString(hexToDecValues[Char.ToUpper(h[0])], 2).PadLeft(4,'0') + Convert.ToString(hexToDecValues[Char.ToUpper(h[1])], 2).PadLeft(4,'0');
    }

    static void translatePseudos(string[] a, int start)
    {
        switch (a[start])
        {
            case "blt":
                input.Add("slt $at, " + a[start+1] + ", " + a[start+2]);
                input.Add("bne $at, $zero, " + a[start+3]);
                break;
            case "bgt":
                input.Add("slt $at, " + a[start+2] + ", " + a[start+1]);
                input.Add("bne $at, $zero, " + a[start+3]);
                break;
            case "ble":
                input.Add("slt $at, " + a[start+2] + ", " + a[start+1]);
                input.Add("beq $at, $zero, " + a[start+3]);
                break;
            case "move":
                input.Add("addi " + a[start+1] + ", " + a[start+2] + ", 0");
                break;
            case "mul":
                input.Add("mult " + a[start+2] + ", " + a[start+3]);
                input.Add("mflo " + a[start+1]);
                break;
            case "li":
                int data = Convert.ToInt32(a[start+2]);
                string hi = Convert.ToString(data << 16);//should shift in 0
                string lo = Convert.ToString((uint)data >> 16);//only shifts in 0 for unsigned
                input.Add("lui " + a[start+1] + ", " + hi);
                input.Add("ori " + a[start+1] + ", " + a[start+1] + ", " + lo);
                break;
            case "la"://NEED TO MAKE SURE TO CHECK FOR LABEL IN LUI AND ORI LATER!!!!!!!!!!!!!!!!!!!!
                input.Add("lui " + a[start+1] + ", " + a[start+2]);
                input.Add("ori " + a[start+1] + ", " + a[start+1] + ", " + a[start+2]);
                break;
            case "b":
                input.Add("beq $zero, $zero, " + a[start+1]);
                break;
            case "clear":
                input.Add("add " + a[start+1] + ", $zero, $zero");
                break;
            case "bge":
                input.Add("slt $at, " + a[start+1] + ", " + a[start+2]);
                input.Add("beq $at, $zero, " + a[start+3]);
                break;
            default://nop
                input.Add("sll $zero, $zero, 0");
                break;
        }
    }

    //Get rid of comments; break up into individual segments; calculate labels; translate pseudoinstructions; store lines in queue
    static void firstPass()
    {
        int currentAddress = DEFAULT_START;

        if (!File.Exists(@"input.txt"))
            System.Console.WriteLine("Please place a file named 'input' in the debug folder contained in this solution.");
        else using (StreamReader sr = File.OpenText(@"input.txt"))
            {
                string rawLine;
                string[] comments;
                while ((rawLine = sr.ReadLine()) != null)
                {
                    //parse line before adding to queue
                    comments = rawLine.Split(new char[] { '#', ';', '/' });

                    if (!(comments[0].StartsWith("#") || comments[0].StartsWith(";") || comments[0].StartsWith("/")) && !string.IsNullOrEmpty(rawLine))//don't add comments
                    {
                        comments[0] = comments[0].Trim();
                        string[] args;

                        if (comments[0].StartsWith(".org"))
                        {
                            input.Add(comments[0]);
                            args = comments[0].Split(new char[] { ' ' });
                            currentAddress = Convert.ToInt32(args[1]);
                        }
                        else if (comments[0].StartsWith(".byte"))
                        {
                            input.Add(comments[0]);
                            args = comments[0].Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 0; i < args.Count(); i++)
                                args[i] = args[i].Trim();
                        }
                        else if (comments[0].StartsWith(".end"))
                            input.Add(comments[0]);
                        else//instruction, pseudo, or label
                        {
                            args = comments[0].Split(new char[] { ':', ' ', ',', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

                            for (int i = 0; i < args.Count(); i++)
                                args[i] = args[i].Trim();//cut out white space of each arg

                            if (instructions.ContainsKey(args[0]))
                                input.Add(comments[0]);
                            else if (pseudos.ContainsKey(args[0]))//pseudoinstruction
                            {
                                translatePseudos(args, 0);
                                currentAddress += 4 * pseudos[args[0]];
                            }
                            else//label
                            {
                                labels.Add(args[0], currentAddress);

                                if(pseudos.ContainsKey(args[1]))
                                {
                                    translatePseudos(args, 1);
                                    currentAddress += 4 * pseudos[args[1]];
                                }
                                else
                                    input.Add(comments[0]);
                            }

                            currentAddress += 4;
                        }
                    }
                }
            }
    }

    //fill in label arguments - need to check for absolute or relative value based on instruction
    //no more pseudoinstructions since they've already been translated
    static void secondPass()
    {
        int currentAddress = DEFAULT_START;
        string[] args;

        //foreach (string line in input)
        for(int j = 0; j < input.Count(); j++)
        {
            string line = input[j];

            if (line.StartsWith(".org"))
            {
                args = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                currentAddress = Convert.ToInt32(args[1]);
            }
            else if (line.StartsWith(".byte"))
            {
                args = line.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < args.Count(); i++)
                    args[i] = args[i].Trim();
            }
            else if (line.StartsWith(".end"))
                break;
            else//(pseudo)instruction, or label
            {
                args = line.Split(new char[] { ':', ' ', ',', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < args.Count(); i++)
                    args[i] = args[i].Trim();//cut out white space of each arg

                string currentInstruct;
                int start;

                if (instructions.ContainsKey(args[0]))
                {
                    start = 0;
                    currentInstruct = args[0];
                }
                else//label
                {
                    start = 1;
                    currentInstruct = args[1];
                }

                if (currentInstruct == "beq" || currentInstruct == "bne")
                {
                    for (int i = start; i < args.Count(); i++)
                    {
                        if (labels.ContainsKey(args[i]))
                            input[j] = line.Replace(args[i], Convert.ToString((labels[args[i]] - (currentAddress + 4))/4));
                    }
                }
                else if (currentInstruct == "j" || currentInstruct == "jal")
                {
                    for (int i = start; i < args.Count(); i++)
                    {
                        if (labels.ContainsKey(args[i]))
                            input[j] = line.Replace(args[i], Convert.ToString(labels[args[i]] / 4));
                    }
                }
                else if (currentInstruct == "lb" || currentInstruct == "lw" || currentInstruct == "sw" || currentInstruct == "sb" || currentInstruct == "lui")
                {
                    for (int i = start; i < args.Count(); i++)
                    {
                        if (labels.ContainsKey(args[i]))
                            input[j] = line.Replace(args[i], Convert.ToString(labels[args[i]] / 4));
                    }
                }

                currentAddress += 4;
            }
        }
    }

    static void getBinary()
    {
        int currentAddress = DEFAULT_START;
        string[] args;

        //foreach (string line in input)
        for(int j = 0; j < input.Count(); j++)
        {
            string line = input[j];
            if (line.StartsWith(".org"))
            {
                args = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                currentAddress = Convert.ToInt32(args[1]);
                addresses.Add(currentAddress);
                input.RemoveAt(j--);
            }
            else if (line.StartsWith(".byte"))
            {
                args = line.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                string bytes = "";

                for (int i = 1; i < args.Count(); i++)
                {
                    args[i] = args[i].Trim();

                    if (args[i].EndsWith("B"))//just remove B on binary
                        args[i] = args[i].Remove(8);
                    else if (args[i].EndsWith("H"))//convert hex to binary string
                        args[i] = hexToBinary(args[i].Remove(2));
                    else//convert decimal to binary string
                        args[i] = Convert.ToString(Convert.ToInt32(args[i]), 2).PadLeft(8, '0');

                    bytes += args[i];

                    if (i % 4 == 0)
                    {
                        binary.Add(bytes);
                        bytes = "";
                        currentAddress += 4;
                        addresses.Add(currentAddress);
                    }
                }

                for (int i = args.Count() - 1; i % 4 != 0; i++)
                    bytes += "00000000";

                if ((args.Count() - 1) % 4 != 0)
                {
                    binary.Add(bytes);
                    currentAddress += 4;
                    addresses.Add(currentAddress);
                }
            }
            else if (line.Contains(".end"))
                binary.Add("00000000000000000000000000000000");
            else//(pseudo)instruction, or label
            {
                args = line.Split(new char[] { ':', ' ', ',', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < args.Count(); i++)
                    args[i] = args[i].Trim();//cut out white space of each arg

                string currentInstruct;
                int start;

                if (instructions.ContainsKey(args[0]))
                {
                    start = 0;
                    currentInstruct = args[0];
                }
                else//label
                {
                    start = 1;
                    currentInstruct = args[1];
                }

                int memory = instructions[currentInstruct].binary;

                switch (instructions[currentInstruct].format)
                {
                    case 'R':
                        if (rBasic.Contains(currentInstruct))
                            memory |= ((registers[args[start + 1]] << 11) | (registers[args[start + 2]] << 21) | (registers[args[start + 3]] << 16));
                        else if (currentInstruct == "jr")
                            memory |= (registers[args[start + 1]] << 21);
                        else if (currentInstruct == "sll" || currentInstruct == "srl")
                            memory |= ((registers[args[start + 1]] << 11) | (registers[args[start + 2]] << 16) | (Convert.ToInt32(args[start + 3]) << 6));
                        break;
                    case 'I':
                        if (iBasic.Contains(currentInstruct))
                        {
                            memory |= (registers[args[start + 1]] << 11) | (registers[args[start + 2]] << 21);

                            if (registers.ContainsKey(args[start + 3]))//reg
                                memory |= registers[args[start + 3]] << 16;
                            else//num (could have been label)
                                memory |= Convert.ToInt32(args[start + 3]) << 16;
                        }
                        else if (currentInstruct == "beq" || currentInstruct == "bne")
                        {
                            //memory |= ((registers[args[start + 1]] << 21) | (registers[args[start + 2]] << 16) | (0x0000ffff & Convert.ToInt32(args[start + 3])));
                            memory |= (registers[args[start + 1]] << 21) | (0x0000ffff & Convert.ToInt32(args[start + 3]));

                            if (registers.ContainsKey(args[start + 2]))
                                memory |= (registers[args[start + 2]] << 16);
                            else
                                memory |= Convert.ToInt32(args[start + 2]) << 16;

                        }
                        else if (currentInstruct == "lui")
                            memory |= registers[args[start + 1]] << 16 | (0x0000ffff & Convert.ToInt32(args[start + 2]));
                        else// if (currentInstruct == "lb" || currentInstruct == "lw" || currentInstruct == "sw" || currentInstruct == "sb")
                            memory |= ((registers[args[start + 1]] << 16) | (0x0000ffff & Convert.ToInt32(args[start + 2])) | (registers[args[start + 3]] << 21));
                        break;
                    case 'J':
                        memory |= Convert.ToInt32(args[args.Count() - 1]);
                        break;
                    default:
                        break;
                }

                binary.Add(Convert.ToString(memory, 2).PadLeft(32, '0'));
                currentAddress += 4;
                addresses.Add(currentAddress);
            }
        }
    }

    static void Main()
    {
        fillDictionaries();
        firstPass();
        secondPass();
        getBinary();

        foreach (string s in binary)
            hex.Add(Convert.ToInt32(s, 2).ToString("X").PadLeft(8,'0'));

        using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"output.txt", true))
        {
            file.WriteLine("Address\t\tBinary Value\t\t\tHex Value\tTranslated Instruction");
            file.WriteLine();

            for (int i = 0; i < addresses.Count(); i++)
                file.WriteLine("{0}\t{1}\t{2}\t{3}", addresses[i], binary[i], hex[i], input[i]);
        }  
    }
}
