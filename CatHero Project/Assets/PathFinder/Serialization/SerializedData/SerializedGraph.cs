using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using K_PathFinder.Graphs;
using System;
using K_PathFinder.CoverNamespace;
using K_PathFinder.CoolTools;

namespace K_PathFinder.Serialization2 {

    [Serializable]
    public class SerializedGraph {
        [SerializeField] public ChunkData chunk;
        [SerializeField] public SerializedCell[] serializedCells;
        [SerializeField] public SerializedCover[] serializedCovers;

        //map
        [SerializeField] public int serializedCellMapResolution;
        [SerializeField] public int[] serializedRichCellMap;
        [SerializeField] public int[] serializedSimpleCellMap;
        [SerializeField] public IndexLengthInt[] serializedRichCellMapLayout;
        [SerializeField] public IndexLengthInt[] serializedSimpleCellMapLayout;
        [SerializeField] public bool serializedSimpleMapHaveEmptySpots;

        //border
        //enum Direction for target index/direction
        //0-3 = border edges that graph was created with
        //4-7 = border edges that appear when graph connected to other graphs
        //8 = coutour of graph that made out of initial edges
        [SerializeField] public CellContentConnectionData[] borderInfo;
        [SerializeField] public IndexLengthInt[] borderInfoLayout;

        //total map
        public IndexLengthInt[] serializedLayout;
        public CellContentData[] serializedEdges;
        public CellConnection[] serializedConnections;
                
        public SerializedGraph(NavmeshSerializer serializer, Graph graph) {
            chunk = graph.chunk;

            //cells
            Cell[] cellsArray;
            int cellsCount;
            graph.GetCells(out cellsArray, out cellsCount);
            serializedCells = new SerializedCell[cellsCount];

            for (int cellID = 0; cellID < cellsCount; cellID++) {
                serializedCells[cellID] = new SerializedCell(serializer, cellsArray[cellID]);
            }

            //covers
            List<Cover> covers = graph.covers;
            serializedCovers = new SerializedCover[covers.Count];

            for (int i = 0; i < covers.Count; i++) {
                serializedCovers[i] = new SerializedCover(covers[i]);
            }

            //map
            Cell[] cellRichMap, cellSimpleMap;
            IndexLengthInt[] cellRichMapLayout, cellSimpleMapLayout;
            int cellMapResolution;
            bool cellSimpleMapHaveEmptySpots;
            graph.GetCellMapToSerialization(out cellMapResolution, out cellRichMap, out cellRichMapLayout, out cellSimpleMap, out cellSimpleMapLayout, out cellSimpleMapHaveEmptySpots);
            int resolutionSize = cellMapResolution * cellMapResolution;


            serializedCellMapResolution = cellMapResolution;
            serializedSimpleMapHaveEmptySpots = cellSimpleMapHaveEmptySpots;
            IndexLengthInt lastRichLayout = cellRichMapLayout[resolutionSize - 1];
            IndexLengthInt lastSimpleLayout = cellSimpleMapLayout[resolutionSize - 1];

            serializedRichCellMapLayout = new IndexLengthInt[resolutionSize];
            serializedSimpleCellMapLayout = new IndexLengthInt[resolutionSize];
            serializedRichCellMap = new int[lastRichLayout.index + lastRichLayout.length];      
            serializedSimpleCellMap = new int[lastSimpleLayout.index + lastSimpleLayout.length];

            for (int i = 0; i < resolutionSize; i++) {
                serializedRichCellMapLayout[i] = cellRichMapLayout[i];
                serializedSimpleCellMapLayout[i] = cellSimpleMapLayout[i];
            }

            for (int i = 0; i < lastRichLayout.index + lastRichLayout.length; i++) {
                serializedRichCellMap[i] = cellRichMap[i].globalID;
            }

            for (int i = 0; i < lastSimpleLayout.index + lastSimpleLayout.length; i++) {
                serializedSimpleCellMap[i] = cellSimpleMap[i].globalID;
            }

            //contour and border
            var borderData = graph.borderData;  
            CellContentConnectionData[] cccd;
            IndexLengthInt[] cccdLayout;
            borderData.GetOptimizedData(out cccd, out cccdLayout);

            int totalBorderLength = cccdLayout[Graph.BORDER_DATA_LENGTH - 1].index + cccdLayout[Graph.BORDER_DATA_LENGTH - 1].length;
            CellContentConnectionData[] borderInfo = new CellContentConnectionData[totalBorderLength];
            IndexLengthInt[] borderInfoLayout = new IndexLengthInt[Graph.BORDER_DATA_LENGTH];

            for (int i = 0; i < totalBorderLength; i++) {
                borderInfo[i] = cccd[i];
            }

            for (int i = 0; i < 10; i++) {
                borderInfoLayout[i] = cccdLayout[i];
            }

            this.borderInfo = borderInfo;
            this.borderInfoLayout = borderInfoLayout;

            //total map    
            CellContentData[] graphEdges;
            CellConnection[] graphConnections;
            IndexLengthInt[] graphLayout;
            graph.GetTotalMap(out graphEdges, out graphConnections, out graphLayout);
            IndexLengthInt lastLayout = graphLayout[cellsCount - 1];

         
            serializedLayout = new IndexLengthInt[cellsCount];
            serializedEdges = new CellContentData[lastLayout.index + lastLayout.length];
            serializedConnections = new CellConnection[lastLayout.index + lastLayout.length];

            for (int c = 0; c < cellsCount; c++) {
                IndexLengthInt layout = graphLayout[c];
                serializedLayout[c] = layout;

                for (int i = 0; i < layout.length; i++) {
                    int index = layout.index + i;
                    serializedEdges[index] = graphEdges[index];
                    serializedConnections[index] = graphConnections[index];                    
                }
            }
        }
    }
}