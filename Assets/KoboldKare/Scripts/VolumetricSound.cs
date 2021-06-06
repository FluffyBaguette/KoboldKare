﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolumetricSound : MonoBehaviour {
    private AudioListener listener;
    private AudioSource source;
    private void Start() {
        source = GetComponent<AudioSource>();
    }
    void Update() {
        if (listener == null || !listener.isActiveAndEnabled ) {
            //listener = GameObject.FindObjectOfType<AudioListener>();
            if (NetworkManager.instance.localPlayerInstance != null) {
                foreach(AudioListener l in NetworkManager.instance.localPlayerInstance.GetComponentsInChildren<AudioListener>()) {
                    if (l.isActiveAndEnabled) {
                        listener = l;
                        break;
                    }
                }
            }
            if (listener == null || !listener.isActiveAndEnabled) {
                return;
            }
        }
        float dist = Vector3.Distance(transform.position, listener.transform.position);
        source.spatialBlend = Mathf.Clamp01((dist - source.minDistance)/(source.maxDistance-source.minDistance));
    }
}
