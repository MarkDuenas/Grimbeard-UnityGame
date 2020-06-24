using K_PathFinder;
using K_PathFinder.CoolTools;
using K_PathFinder.Graphs;
using K_PathFinder.Pool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace K_PathFinder {
    //local avoidance agent
    //used with local avoidance to define where something should be evaded and how it should react to other agents
    public class LocalAvoidanceAgent {
        public AgentProperties properties;

        //velocity obstacle variables  
        public float maxAgentVelocity = 2f;
        public float avoidanceResponsibility = 0.5f; // bigger number - move inclined agent to evade
        public float careDistance = 0.75f; //how fast object should react to obstacle. 0 - instant, 1 - only if it collide/ range is in 0.01f - 0.99f
        public int maxNeighbors = 10; //The default maximum number of other agents a new agent takes into account in the navigation.
        public float maxNeighbourDistance = 10f; //how far agent should check if object are suitable for avoidance
        public bool avoidNavmeshBorders = true;
        private Vector2 _velocity, _preferableVelocity, _safeVelocity;
        private Vector3 _position;

        //one side evasion
        public bool preferOneSideEvasion = false;
        public float preferOneSideEvasionOffset = 2f;

        //dead lock variables
        public bool useDeadLockFailsafe = false;
        public float deadLockVelocityThreshold = 0.025f;
        public float deadLockFailsafeVelocity = 0.3f;
        public float deadLockFailsafeTime = 2f;//in seconds
        public DateTime deadLockTriggeredTime; //last deadLock
        public int layerMask = 1;

        public LocalAvoidanceAgent(AgentProperties properties) {
            this.properties = properties;
        }

        public void SetBasicSettings(float maxVelocity, float avoidanceResponsibility, float careDistance, int maxNeighbors, float maxNeighbourDistance, int layerMask) {
            this.maxAgentVelocity = maxVelocity;
            this.avoidanceResponsibility = avoidanceResponsibility;
            this.careDistance = careDistance;
            this.maxNeighbors = maxNeighbors;
            this.maxNeighbourDistance = maxNeighbourDistance;
            this.layerMask = layerMask;
        }

        public void SetDeadLockSettings(bool useDeadLockFailsafe, float deadLockVelocityThreshold, float deadLockFailsafeVelocity, float deadLockFailsafeTime) {
            this.useDeadLockFailsafe = useDeadLockFailsafe;
            this.deadLockVelocityThreshold = deadLockVelocityThreshold;
            this.deadLockFailsafeVelocity = deadLockFailsafeVelocity;
            this.deadLockFailsafeTime = deadLockFailsafeTime;
        }

        public void SetSidePreference(bool preferOneSideEvasion, float preferOneSideEvasionOffset) {
            this.preferOneSideEvasion = preferOneSideEvasion;
            this.preferOneSideEvasionOffset = preferOneSideEvasionOffset;
        }

        public float radius {
            get { return properties.radius; }
        }

        public Vector3 position {
            get { lock (this) return _position; }
            set { lock (this) _position = value; }
        }
        public Vector2 velocity {
            get { lock (this) return _velocity; }
            set { lock (this) _velocity = value; }
        }
        public Vector2 preferableVelocity {
            get { lock (this) return _preferableVelocity; }
            set { lock (this) _preferableVelocity = value; }
        }
        public Vector2 safeVelocity {
            get { lock (this) return _safeVelocity; }
            set { lock (this) _safeVelocity = value; }
        }

        //shortcuts
        private static Vector2 ToVector2(Vector3 v3) {
            return new Vector2(v3.x, v3.z);
        }
        private static Vector3 ToVector3(Vector2 v2) {
            return new Vector3(v2.x, 0, v2.y);
        }

        public PathFinder.LocalAvoidanceAgentStruct GetStruct(out Vector3 pos, out AgentProperties properties) {
            lock (this) {
                pos = _position;
                properties = this.properties;
                return new PathFinder.LocalAvoidanceAgentStruct(layerMask, properties.radius, properties.height, avoidanceResponsibility, careDistance, _velocity, _preferableVelocity, maxAgentVelocity, maxNeighbors, maxNeighbourDistance, preferOneSideEvasion, preferOneSideEvasionOffset);
            }
        }

        public PathFinder.LocalAvoidanceAgentDeadlock GetDeadlock() {
            lock (this) {
                return new PathFinder.LocalAvoidanceAgentDeadlock(deadLockVelocityThreshold, deadLockFailsafeVelocity, deadLockFailsafeTime, deadLockTriggeredTime);
            }
        }
    }
}