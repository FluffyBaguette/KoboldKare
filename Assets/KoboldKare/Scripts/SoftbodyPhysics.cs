﻿
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SoftbodyPhysics : MonoBehaviour {
    public enum UpdateType {
        FixedUpdate,
        Update,
        LateUpdate,
    }
    public float timeAccumulator;
    public UpdateType updateMode = UpdateType.LateUpdate;
    public float randomization = 0.1f;
    [System.Serializable]
    public class SoftbodyZone {
        public Transform origin;
        public float radius;
        public float amplitude;
        public float elastic;
        public float friction;
        public Vector3 gravity;
        public Color colorMask;
        [HideInInspector]
        public Vector3 lastPosition;
        [HideInInspector]
        public Vector3 lastVelocityGuess;
        [HideInInspector]
        public Vector3 velocity;
        [HideInInspector]
        public Vector3 virtualPos;
        public void Friction(float dt) {
            float speed = velocity.magnitude;
            if (speed<Mathf.Epsilon) {
                return;
            }
            float drop = friction * speed * dt;
            float newSpeed = speed - drop;
            if (newSpeed<0) {
                newSpeed = 0;
            }
            newSpeed /= speed;
            velocity *= newSpeed;
        }
        public void Gravity(float dt, float scale) {
            velocity += gravity * dt * scale;
        }
        public void Acceleration(float dt) {
            Vector3 velocityGuess = (origin.position - lastPosition)/dt;
            velocity += (velocityGuess-lastVelocityGuess);
            lastVelocityGuess = velocityGuess;

            velocity -= virtualPos * elastic * dt * 100f;
            virtualPos += velocity * dt;
            lastPosition = origin.position;
        }
        public void Pack(ref Vector4[] packTarget, SkinnedMeshRenderer r, int index, float scale) {
            packTarget[index * 3] = colorMask;
            packTarget[index * 3 + 1] = r.rootBone.InverseTransformPoint(origin.position)*scale;
            packTarget[index * 3 + 1].w = origin.lossyScale.y*radius;
            packTarget[index * 3 + 2] = -r.rootBone.InverseTransformVector(virtualPos);
            packTarget[index * 3 + 2].w = amplitude * scale;
        }
        public void Draw() {
            Gizmos.color = new Color(colorMask.r, colorMask.g, colorMask.b, Mathf.Clamp(colorMask.r + colorMask.g + colorMask.b + colorMask.a, 0f, 0.5f));
            Gizmos.DrawSphere(origin.position, origin.lossyScale.y*radius);
        }
    }
    private Vector4[] vectorsToSend;
    public List<SkinnedMeshRenderer> targetRenderers;
    private Dictionary<SkinnedMeshRenderer, MaterialPropertyBlock> blockCache;
    public List<SoftbodyZone> zones;
    public MaterialPropertyBlock GetBlock(SkinnedMeshRenderer r) {
        if (!blockCache.ContainsKey(r)) {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            blockCache.Add(r, block);
        }
        return blockCache[r];
    }
    public void Awake() {
        foreach (SoftbodyZone zone in zones) {
            zone.amplitude *= UnityEngine.Random.Range(1f - randomization, 1f + randomization);
            zone.elastic *= UnityEngine.Random.Range(1f - randomization, 1f + randomization);
            zone.friction *= UnityEngine.Random.Range(1f - randomization, 1f + randomization);
        }
    }
    public void Start() {
        vectorsToSend = new Vector4[24];
        blockCache = new Dictionary<SkinnedMeshRenderer, MaterialPropertyBlock>();
        foreach (SkinnedMeshRenderer r in targetRenderers) {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            blockCache.Add(r, block);
        }
        foreach( SoftbodyZone zone in zones) {
            zone.lastPosition = zone.origin.position;
        }
    }
    public void LateUpdate() {
        if (updateMode != UpdateType.LateUpdate || Time.deltaTime == 0f) {
            return;
        }
        float frameTime = Mathf.Min(Time.deltaTime,Time.maximumDeltaTime);
        timeAccumulator += frameTime;
        while (timeAccumulator >= Time.fixedDeltaTime) {
            Process(Time.fixedDeltaTime);
            timeAccumulator -= Time.fixedDeltaTime;
        }
        Process(timeAccumulator);
        timeAccumulator = 0f;
        SendData();
    }
    public void FixedUpdate() {
        if (updateMode != UpdateType.FixedUpdate || Time.deltaTime == 0f) {
            return;
        }
        Process(Time.deltaTime);
        SendData();
    }
    public void Update() {
        if (updateMode != UpdateType.Update || Time.deltaTime == 0f) {
            return;
        }
        float frameTime = Mathf.Min(Time.deltaTime,Time.maximumDeltaTime);
        timeAccumulator += frameTime;
        while (timeAccumulator >= Time.fixedDeltaTime) {
            Process(Time.fixedDeltaTime);
            timeAccumulator -= Time.fixedDeltaTime;
        }
        Process(timeAccumulator);
        timeAccumulator = 0f;
        SendData();
    }
    public void Process(float dt) {
        if (dt == 0f) {
            return;
        }
        foreach (SoftbodyZone zone in zones) {
            zone.Friction(dt);
            zone.Gravity(dt, transform.lossyScale.x);
            zone.Acceleration(dt);
        }
    }
    public void SendData() {
        foreach (SkinnedMeshRenderer r in targetRenderers) {
            if (!r.isVisible) {
                continue;
            }
            int i = 0;
            foreach (SoftbodyZone zone in zones) {
                zone.Pack(ref vectorsToSend, r, i++, transform.lossyScale.x);
            }
            GetBlock(r).SetVectorArray("_SoftbodyArray", vectorsToSend);
            r.SetPropertyBlock(GetBlock(r));
        }
    }
    public void OnDrawGizmosSelected() {
        if (zones == null) {
            return;
        }
        foreach (SoftbodyZone zone in zones) {
            zone.Draw();
        }
    }
    public Vector3 TransformPoint(Vector3 wpos, Color color) {
        Vector3 offset = Vector3.zero;
        foreach(SoftbodyZone zone in zones) {
            float mask = Mathf.Clamp01(Vector4.Dot(color, zone.colorMask));
            float dist = Vector3.Distance(targetRenderers[0].rootBone.InverseTransformPoint(wpos), targetRenderers[0].rootBone.InverseTransformPoint(zone.origin.position)) / (zone.radius*zone.origin.lossyScale.x);
            float effect = Mathf.Clamp01(1f - dist * dist) * mask;
            offset -= zone.virtualPos * targetRenderers[0].rootBone.lossyScale.x * effect * zone.amplitude;
        }
        return wpos + offset;
    }
}
