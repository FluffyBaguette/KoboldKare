﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KoboldKare;
using Photon.Pun;

[CreateAssetMenu(fileName = "ReagentCallbacks", menuName = "Data/Reagent Callbacks", order = 1)]
public class ReagentSerializableCallbacks : ScriptableObject {
    //private class ReagentProcessPair {
    [NonSerialized]
    private Dictionary<GameObject, List<Task>> reagentProcesses = new Dictionary<GameObject, List<Task>>();
    //[NonSerialized]
    //private HashSet
    public GameObject explosionPrefab;
    public AudioClip sizzleSound;
    public Material scorchDecal;
    public LayerMask playerMask;
    public AudioClip reagentReactionSound;
    private void AddProcess(GameObject obj, Task t, string name) {
        if (!reagentProcesses.ContainsKey(obj)) {
            reagentProcesses.Add(obj, new List<Task>());
        }
        t.name = name;
        reagentProcesses[obj].Add(t);
        // Make sure we clean ourselves up.
        t.Finished += (manual) => ClearProcess(obj, t);
    }
    private bool HasProcess(GameObject obj, string name) {
        if (!reagentProcesses.ContainsKey(obj)) {
            return false;
        }
        for (int i = 0; i < reagentProcesses[obj].Count; i++) {
            if (reagentProcesses[obj][i].name == name && reagentProcesses[obj][i].Running) {
                return true;
            }
        }
        return false;
    }
    private void ClearProcess(GameObject obj, Task t) {
        if (!reagentProcesses.ContainsKey(obj)) {
            return;
        }
        reagentProcesses[obj].Remove(t);
        if (reagentProcesses[obj].Count == 0) {
            reagentProcesses.Remove(obj);
        }
    }
    private void ClearProcesses(GameObject obj, string name) {
        if (!reagentProcesses.ContainsKey(obj)) {
            return;
        }
        for (int i=0;i<reagentProcesses[obj].Count;i++) {
            if (reagentProcesses[obj][i].name == name) {
                reagentProcesses[obj][i].Stop();
                reagentProcesses[obj].RemoveAt(i);
            }
        }
    }

    public IEnumerator ReagentReactionSound(GameObject obj) {
        Transform targetTransform = obj.transform;
        IGrabbable grabbable = obj.GetComponent<IGrabbable>();
        if (grabbable != null) {
            targetTransform = grabbable.GrabTransform(grabbable.GetRigidBodies()[0]);
        }
        GameManager.instance.SpawnAudioClipInWorld(reagentReactionSound, targetTransform.position, 1f);
        yield return new WaitForSeconds(1f);
    }

    public IEnumerator SizzleThenExplode(float delay, GameObject obj, ReagentContents contents) {
        Transform targetTransform = obj.transform;
        IGrabbable grabbable = obj.GetComponent<IGrabbable>();
        if (grabbable != null) {
            targetTransform = grabbable.GrabTransform(grabbable.GetRigidBodies()[0]);
        }
        GameManager.instance.SpawnAudioClipInWorld(sizzleSound, targetTransform.position, 1.1f, GameManager.instance.soundEffectLoudGroup);
        Vector3 backupPosition = targetTransform.position;
        // We periodically grab a backup spot, just in case the prop gets removed over the network right before the explosion.
        yield return new WaitForSeconds(delay/4);
        if (targetTransform != null) {
            backupPosition = targetTransform.position;
        }
        yield return new WaitForSeconds(delay/4);
        if (targetTransform != null) {
            backupPosition = targetTransform.position;
        }
        yield return new WaitForSeconds(delay/4);
        if (targetTransform != null) {
            backupPosition = targetTransform.position;
        }
        yield return new WaitForSeconds(delay/4);
        if (targetTransform != null) {
            backupPosition = targetTransform.position;
        }
        GameObject.Instantiate(explosionPrefab, backupPosition, Quaternion.identity);
        HashSet<Kobold> foundKobolds = new HashSet<Kobold>();
        foreach( Collider c in Physics.OverlapSphere(backupPosition, 5f, playerMask, QueryTriggerInteraction.Ignore)) {
            GameManager.instance.SpawnDecalInWorld(scorchDecal, backupPosition+Vector3.up*4, Vector3.down, Vector2.one * 20f, Color.black, c.gameObject, 8f, false);
            Kobold k = c.GetComponentInParent<Kobold>();
            if (k != null && !foundKobolds.Contains(k)) {
                foundKobolds.Add(k);
                foreach (Rigidbody r in k.ragdollBodies) {
                    r.AddExplosionForce(3000f, backupPosition, 5f);
                }
                k.body.AddExplosionForce(3000f, backupPosition, 5f);
                k.KnockOver(6f);
            } else {
                Rigidbody r = c.GetComponentInParent<Rigidbody>();
                r?.AddExplosionForce(3000f, backupPosition, 5f);
            }
            GenericDamagable damagable = c.GetComponentInParent<GenericDamagable>();
            // Bombs hurt!!
            if (damagable != null) {
                float dist = Vector3.Distance(backupPosition, c.ClosestPoint(backupPosition));
                float damage = Mathf.Clamp01((5f - dist) / 5f) * 250f;
                //linear falloff because :shrug:
                damagable.Damage(damage);
            }
        }
        // Remove all explosium
        if (targetTransform != null) {
            contents.Empty();
        }
    }

    // To prevent rapid execution, we use coroutines to wait until the user is done mixing things before it decides to blow.
    public void SpawnExplosion(ReagentContents contents) {
        GameObject obj = contents.gameObject;
        if (obj == null) {
            return;
        }
        if (!HasProcess(obj, "Explosion")) {
            AddProcess(obj, new Task(SizzleThenExplode(4f, obj, contents)), "Explosion");
        }
    }
    public void BubbleSound(ReagentContents contents) {
        GameObject obj = contents.gameObject;
        if (obj == null) {
            return;
        }
        if (!HasProcess(obj, "Bubbles")) {
            AddProcess(obj, new Task(ReagentReactionSound(obj)), "Bubbles");
        }
    }
    public void PrintSomething(string thing) {
        Debug.Log("thing");
    }
    public void DestroyThing(UnityEngine.Object g) {
        if (g is GameObject) {
            PhotonView other = ((GameObject)g).GetComponentInParent<PhotonView>();
            if (other != null && other.IsMine) {
                SaveManager.Destroy(other.gameObject);
                return;
            }
        } else {
            Destroy(g);
        }
    }
}
