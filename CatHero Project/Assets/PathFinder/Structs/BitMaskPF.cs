using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder {
    //https://en.wikipedia.org/wiki/Mask_(computing)
    [System.Serializable]
    public struct BitMaskPF {
        [SerializeField] public int value;  //array of bool[32]

        //constructor to set mask with single value
        public BitMaskPF(int value) {
            this.value = value;
        }

        //constructor to set it using params
        public BitMaskPF(params bool[] values) {
            value = 0;
            for (int i = 0; i < (values.Length > 32 ? 32 : values.Length); i++) {
                this[i] = values[i];
            }
        }

        /// <summary>
        /// access bit by index
        /// </summary>
        public bool this[int index] {
            set { this.value = value ? (this.value | (1 << index)) : (this.value & ~(1 << index));}
            get { return ((1 << index) & value) != 0; }
        }

        //implicit convertation to Int32
        public static implicit operator int(BitMaskPF mask) {
            return mask.value;
        }

        //implicit convertation Int32 to bitmask so you can set it as BitMaskPF mask = someInt;
        public static implicit operator BitMaskPF(int integer) {
            return new BitMaskPF(integer);
        }
    }   
}