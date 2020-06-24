using System;
using K_PathFinder.Graphs;

namespace K_PathFinder {
    //old stuff. will be removed at some point but right now serve as example

    //public class PathFinderTemplateChunkDisconnection : PathFinderTemplate {       
    //    Graph graph;
    //    Action callBack = null;

    //    public PathFinderTemplateChunkDisconnection(Graph graph) : base(graph.gridPosition, graph.properties) {
    //        this.graph = graph;
    //    }

    //    public void SetCallBack(Action callBack) {
    //        this.callBack = callBack;
    //    }

    //    public override void Work() {
    //        lock (graph) {
    //            graph.SetAsCanNotBeUsed();
    //            graph.FunctionToDisconnectGraphFromNeightboursInPathfinderThread();
    //        }

    //        if(callBack != null)
    //            callBack.Invoke();
    //    }
    //}

}
