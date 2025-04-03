using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu(fileName = "ColoredHeadersData", menuName = "ColoredHeaders", order = 99)]
public class COLOREDHEADERS_SO : ScriptableObject
{

    [Serializable]
    public struct ColoredTitle
    {
        public string name;
        public Color color;

        public ColoredTitle(string _name, Color _color)
        {
            this.name = _name;
            this.color = _color;
        }
    }


        [SerializeField]
        public List<ColoredTitle> coloredTitles;
}
