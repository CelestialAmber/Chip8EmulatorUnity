using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;
using System.Linq;
using TMPro;

[RequireComponent(typeof(AudioSource))]
public class Main : MonoBehaviour{
    byte[] characterData = {
        0xF0,0x90,0x90,0x90,0xF0,//0
        0x20,0x60,0x20,0x20,0x70,//1
        0xF0,0x10,0xF0,0x80,0xF0,//2
        0xF0,0x10,0xF0,0x10,0xF0,//3
        0x90,0x90,0xF0,0x10,0x10,//4
        0xF0,0x80,0xF0,0x10,0xF0,//5
        0xF0,0x80,0xF0,0x90,0xF0,//6
        0xF0,0x10,0x30,0x40,0x40,//7
        0xF0,0x90,0xF0,0x90,0xF0,//8
        0xF0,0x90,0xF0,0x10,0xF0,//9
        0xF0,0x90,0xF0,0x90,0x90,//A
        0xE0,0x90,0xE0,0x90,0xE0,//B
        0xF0,0x80,0x80,0x80,0xF0,//C
        0xE0,0x90,0x90,0x90,0xE0,//D
        0xF0,0x80,0xF0,0x80,0xF0,//E
        0xF0,0x80,0xF0,0x80,0x80//F
    };

    public byte[] ram = new byte[4096];

    public Texture2D screenTex;

    public RawImage screenImage;
    public string romName;
    public int delayTimer, soundTimer;

    public bool[] isPressingKey = new bool[16];

    public byte[] V = new byte[16];

    public ushort pc, I;

    public int sp;

    public bool loadRomAtStart;

    public bool waitForInput;
    bool waitForVBlank;
    public int waitForInputRegisterIndex;

    public bool showDebugMessages;
    public ushort[] stack = new ushort[16];

    public int cycleSpeed = 500; //default speed is 500hz

    public bool isRunning = false;

    string romPath;

    public bool enableSound;

    public TMP_Text soundEnabledText;

    byte[] romBytes;

    public TMP_InputField romInputField;
    bool useOldInstructions;

    // Start is called before the first frame update
    void Start(){
        screenTex = new Texture2D(64, 32);
        screenTex.filterMode = FilterMode.Point;
        ClearScreen();
        screenImage.texture = screenTex;
        if (loadRomAtStart)
        {
            LoadNewRom(romName);
            Init();
        }
    }

    public void ToggleUseOldInstructions(){
        useOldInstructions = !useOldInstructions;
    }


    public void LoadRomButton(){
        LoadNewRom(romInputField.text);
        Init();
    }

    public void Reset(){
        Init();
    }

    public void LoadNewRom(string name){
        try
        {
            romPath = Application.streamingAssetsPath + "/Chip-8 Roms/" + name;
            romBytes = File.ReadAllBytes(romPath);
        }
        catch (Exception)
        {
            throw new UnityException("The given ROM path doesn't exist");
        }
    }

    void Init(){
        for (int i = 0; i < 4096; i++){
            ram[i] = 0;
        }
        for (int i = 0; i < 16; i++){
            V[i] = 0;
            stack[i] = 0;
            isPressingKey[i] = false;
        }
        missedCycles = 0;
        waitForInput = false;
        waitForInputRegisterIndex = 0;
        sp = 0;
        I = 0;
        delayTimer = 0;
        soundTimer = 0;
        pc = 0x200;
        Array.Copy(characterData, ram, characterData.Length);
        Array.Copy(romBytes, 0, ram, 0x200, romBytes.Length);
        ClearScreen();
        isRunning = true;
    }

    void ClearScreen(){
        screenTex.SetPixels(0, 0, 64, 32, Enumerable.Repeat(Color.black, 64 * 32).ToArray());
        screenTex.Apply();
    }

    float missedCycles;
    int spriteXTemp, spriteYTemp, spriteHeightTemp;

    // Update is called once per frame
    void Update(){
        if (Input.GetKeyDown(KeyCode.B)) enableSound = !enableSound;
        soundEnabledText.text = "Sound: " + (enableSound ? "Enabled" : "Disabled");

        if (isRunning){
            if (!romInputField.isFocused){
                HandleInput();
                if (Input.GetKeyDown(KeyCode.T)) Reset();
            }

            float cycles = (float)cycleSpeed / 60f;
            missedCycles += cycles - Mathf.Floor(cycles);

            if (Mathf.Approximately(missedCycles, 1f))
            {
                missedCycles = 0;
                cycles++;
            }

            for (int i = 0; i < Mathf.FloorToInt(cycles); i++){ //divide the cycle speed by 60 to get the correct cycle number
                if (!waitForInput && !waitForVBlank) ExecuteInstruction();
            }

            if(waitForVBlank){
                waitForVBlank = false;
                DrawSprite();
            }

            if (delayTimer > 0) delayTimer--;
            if (soundTimer > 0) soundTimer--;
        }
    }

    public KeyCode[] keys = {
    KeyCode.X,//0
	KeyCode.Alpha1,//1
	KeyCode.Alpha2,//2
	KeyCode.Alpha3,//3
	KeyCode.Q,//4
	KeyCode.W,//5
	KeyCode.E,//6
	KeyCode.A,//7
	KeyCode.S,//8
	KeyCode.D,//9
	KeyCode.Z,//A
	KeyCode.C,//B
    KeyCode.Alpha4,//C
	KeyCode.R,//D
	KeyCode.F,//E
	KeyCode.V//F
    };

    /*
    Keyboard Value Layout:
    1 2 3 C
    4 5 6 D
    7 8 9 E
    A 0 B F
	*/


    void HandleInput(){
        for (int i = 0; i < 16; i++){
            isPressingKey[i] = Input.GetKey(keys[i]);
            if (waitForInput && isPressingKey[i]){
                waitForInput = false;
                V[waitForInputRegisterIndex] = (byte)i;
                pc += 2;
            }
        }
    }

    float frequency = 440;

    void OnAudioFilterRead(float[] data, int channels){
        if (soundTimer > 0 && enableSound){
            for (int i = 0; i < data.Length; i++){
                data[i] = Mathf.Sign(Mathf.Sin(Mathf.PI * 2f * i * frequency / 44100)) / 4f;
                if (channels == 2){
                    data[i + 1] = data[i];
                    i++;
                }
            }
        }
    }

    void ExecuteInstruction(){
        int opcode = (ram[pc] << 8) + ram[pc + 1];
        int nnn = opcode & 0xFFF;
        int xIndex = (opcode & 0x0F00) >> 8;
        int yIndex = (opcode & 0x00F0) >> 4;
        int n = opcode & 0x0F;
        byte kk = (byte)(opcode & 0xFF);
        byte Vx = V[xIndex];
        byte Vy = V[yIndex];

        if (showDebugMessages){
            Debug.Log("0x" + opcode.ToString("X4") + ", PC = 0x" + pc.ToString("X") + ", I = 0x" + I.ToString("X") + ", nnn = 0x" + nnn.ToString("X") + ", kk = 0x" + kk.ToString("X") + ", Vx = " + xIndex.ToString("X") +
            ", Vy = " + yIndex.ToString("X"));

            string vRegistersString = "";
            for (int i = 0; i < 16; i++){
                vRegistersString += "V" + i.ToString("X") + "= 0x" + V[i].ToString("X") + (i < 15 ? ", " : "");
            }
            Debug.Log(vRegistersString);
        }

        switch (opcode >> 12){//check the first nibble of the 2 byte instruction
            case 0x00:
                switch (opcode & 0xFF){
                    case 0xE0: //clear the display
                        if (showDebugMessages) Debug.Log("cls");
                        ClearScreen();
                        pc += 2;
                        break;
                    case 0xEE: //return from the current subroutine
                        if (showDebugMessages) Debug.Log("ret");
                        sp--;
                        pc = stack[sp];
                        pc += 2;
                        break;
                    default:
                        throw new UnityException("Invalid/unimplemented instruction");
                }
                break;
            case 0x01: //jump to address nnn
                if (showDebugMessages) Debug.Log("jp nnn");
                pc = (ushort)(nnn);
                break;
            case 0x02: //call subroutine at nnn
                if (showDebugMessages) Debug.Log("call nnn");
                stack[sp] = pc;
                sp++;
                pc = (ushort)(nnn);
                break;
            case 0x03: //skip next instruction if Vx = 2nd byte
                if (showDebugMessages) Debug.Log("se Vx, kk");
                if (Vx == kk) pc += 2;
                pc += 2;
                break;
            case 0x04: //skip next instruction if Vx != 2nd byte
                if (showDebugMessages) Debug.Log("sne Vx, kk");
                if (Vx != kk) pc += 2;
                pc += 2;
                break;
            case 0x05: //skip next instruction if Vx = Vy
                if (showDebugMessages) Debug.Log("se Vx, Vy");
                if (Vx == Vy) pc += 2;
                pc += 2;
                break;
            case 0x06:
                if (showDebugMessages) Debug.Log("ld Vx, kk");
                V[xIndex] = kk;
                pc += 2;
                break;
            case 0x07:
                if (showDebugMessages) Debug.Log("add Vx, kk");
                V[xIndex] += kk;
                pc += 2;
                break;
            case 0x08:
                switch (ram[pc + 1] & 0x0F){
                    case 0x00:
                        if (showDebugMessages) Debug.Log("ld Vx, Vy");
                        V[xIndex] = Vy;
                        pc += 2;
                        break;
                    case 0x01:
                        if (showDebugMessages) Debug.Log("or Vx, Vy");
                        V[xIndex] = (byte)(Vx | Vy);
                        V[0x0F] = 0;
                        pc += 2;
                        break;
                    case 0x02:
                        if (showDebugMessages) Debug.Log("and Vx, Vy");
                        V[xIndex] = (byte)(Vx & Vy);
                        V[0x0F] = 0;
                        pc += 2;
                        break;
                    case 0x03:
                        if (showDebugMessages) Debug.Log("xor Vx, Vy");
                        V[xIndex] = (byte)(Vx ^ Vy);
                        V[0x0F] = 0;
                        pc += 2;
                        break;
                    case 0x04:
                        if (showDebugMessages) Debug.Log("add Vx, Vy");
                        V[xIndex] += Vy;
                        V[0x0F] = (byte)(Vx + Vy > 255 ? 1 : 0);
                        pc += 2;
                        break;
                    case 0x05:
                        if (showDebugMessages) Debug.Log("sub Vx, Vy");
                        V[0x0F] = (byte)(Vx > Vy ? 1 : 0);
                        V[xIndex] -= Vy;
                        pc += 2;
                        break;
                    case 0x06:
                        if (showDebugMessages) Debug.Log("shr Vx");
                        V[0x0F] = (byte)(Vx & 0x01);
                        if(useOldInstructions){
                            V[xIndex] = (byte)(V[yIndex] >> 1);
                        }else{
                            V[xIndex] >>= 1;
                        }
                        pc += 2;
                        break;
                    case 0x07:
                        if (showDebugMessages) Debug.Log("subn Vx, Vy");
                        V[0x0F] = (byte)(Vy > Vx ? 1 : 0);
                        V[xIndex] = (byte)(Vy - Vx);
                        pc += 2;
                        break;
                    case 0x0E:
                        if (showDebugMessages) Debug.Log("shl Vx");
                        V[0x0F] = (byte)(Vx >> 7);

                        if(useOldInstructions){
                            V[xIndex] = (byte)(V[yIndex] << 1);
                        }else{
                            V[xIndex] <<= 2;
                        }
                        pc += 2;
                        break;
                    default:
                        throw new UnityException("Invalid/unimplemented instruction");
                }
                break;
            case 0x09:
                if (showDebugMessages) Debug.Log("sne Vx, Vy");
                if (Vx != Vy) pc += 2;
                pc += 2;
                break;
            case 0x0A:
                if (showDebugMessages) Debug.Log("ld I, nnn");
                I = (ushort)(nnn);
                pc += 2;
                break;
            case 0x0B:
                if (showDebugMessages) Debug.Log("jp V0, nnn");
                pc = (ushort)(nnn + V[0]);
                break;
            case 0x0C:
                if (showDebugMessages) Debug.Log("rnd Vx, kk");
                V[xIndex] = (byte)(UnityEngine.Random.Range(0, 255) & kk);
                pc += 2;
                break;
            case 0x0D:
                if (showDebugMessages) Debug.Log("drw, Vx, Vy, n");
                //Save the parameters for later and wait until vblank to draw the sprite
                spriteXTemp = V[xIndex];
                spriteYTemp = V[yIndex];
                spriteHeightTemp = n;
                waitForVBlank = true;
                break;
            case 0x0E:
                switch (opcode & 0xFF){
                    case 0x9E:
                        if (showDebugMessages) Debug.Log("skp Vx");
                        if (isPressingKey[Vx]) pc += 2;
                        pc += 2;
                        break;
                    case 0xA1:
                        if (showDebugMessages) Debug.Log("sknp Vx");
                        if (!isPressingKey[Vx]) pc += 2;
                        pc += 2;
                        break;
                    default:
                        throw new UnityException("Invalid/unimplemented instruction");
                }
                break;
            case 0x0F:
                switch (opcode & 0xFF){
                    case 0x07:
                        if (showDebugMessages) Debug.Log("ld Vx, DT");
                        V[xIndex] = (byte)delayTimer;
                        pc += 2;
                        break;
                    case 0x0A:
                        if (showDebugMessages) Debug.Log("ld Vx, K");
                        waitForInput = true;
                        waitForInputRegisterIndex = xIndex;
                        break;
                    case 0x15:
                        if (showDebugMessages) Debug.Log("ld DT, Vx");
                        delayTimer = Vx;
                        pc += 2;
                        break;
                    case 0x18:
                        if (showDebugMessages) Debug.Log("ld ST, Vx");
                        soundTimer = Vx;
                        pc += 2;
                        break;
                    case 0x1E:
                        if (showDebugMessages) Debug.Log("add I, Vx");
                        I += Vx;
                        pc += 2;
                        break;
                    case 0x29:
                        if (showDebugMessages) Debug.Log("ld F, Vx");
                        I = (ushort)(Vx * 5);
                        pc += 2;
                        break;
                    case 0x33: //store the binary coded decimal representation of Vx in 3 bytes, starting from the leftmost digit
                        if (showDebugMessages) Debug.Log("ld B, Vx");
                        ram[I] = (byte)((Vx / 100) % 10);
                        ram[I + 1] = (byte)((Vx / 10) % 10);
                        ram[I + 2] = (byte)(Vx % 10);
                        pc += 2;
                        break;
                    case 0x55:
                        if (showDebugMessages) Debug.Log("ld [I], Vx");

                        for (int i = 0; i <= xIndex; i++){
                            if(useOldInstructions){
                                ram[I] = V[i];
                                I++;
                            }else{
                                ram[I + i] = V[i];
                            }
                        }
                        pc += 2;
                        break;
                    case 0x65:
                        if (showDebugMessages) Debug.Log("ld Vx, [I]");
                        for (int i = 0; i <= xIndex; i++){
                            if(useOldInstructions){
                                V[i] = ram[I];
                                I++;
                            }else{
                                V[i] = ram[I + 1];
                            }
                        }
                        pc += 2;
                        break;
                    default:
                        throw new UnityException("Invalid/unimplemented instruction");
                }
                break;
            default:
                throw new UnityException("Invalid/unimplemented instruction");
        }
    }

     void DrawSprite(){
            int spriteX = spriteXTemp;
            int spriteY = spriteYTemp;
            int spriteHeight = spriteHeightTemp;
            V[0x0F] = 0;
            
            for (int x = 0; x < 8; x++){
                for (int y = 0; y < spriteHeight; y++){
                    int tempX = x + spriteX, tempY = y + spriteY;
                    //Skip any offscreen pixels
                    if (tempX > 63 || tempY > 31) continue;
                    tempY = 31 - tempY;
                    int lastPixelColor = (int)screenTex.GetPixel(tempX, tempY).r;
                    int col = (ram[I + y] >> (7 - x)) & 1;
                    col ^= lastPixelColor;
                    if (col == 0 && lastPixelColor == 1 && V[0x0F] == 0) V[0x0F] = 1;
                    screenTex.SetPixel(tempX, tempY, new Color(col, col, col, 1));
                }
            }
            screenTex.Apply();
            pc += 2;
        }
}
