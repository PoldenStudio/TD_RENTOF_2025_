using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class LightData : MonoBehaviour
{
    public string PathToChans;
    static LightData instance;

    private Dictionary<int, List<byte>> chan_data = new Dictionary<int, List<byte>>();

    //0.1, 0.1, 0.1 .... 0.1 (7 chanels)
    //0.1, 0.1, 0.1 .... 0.1 (7 chanels)
    //0.1, 0.1, 0.1 .... 0.1 (7 chanels)

    private void Awake()
    {
        if(instance == null)
            instance = this;
    }

    public ByteData byteData;

    public struct ByteData
    {
        public List<byte> Brightness;
        public List<byte> ColorR;
        public List<byte> ColorG;
        public List<byte> ColorB;
        public List<byte> ColorA;
    }

    private void Parse()
    {
        var file = File.ReadAllLines(PathToChans);

        foreach (var line in file)
        {
            var splitted_line = line.Split(',');
            for (int i = 0; i < splitted_line.Length; i++)
            {
                chan_data.Add(i, new List<byte>());
            }
        }

        //Проходимся по файла и записываем в byteData
    }
}
