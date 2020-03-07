using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainCamera : MonoBehaviour {
    void Update() {
        GameObject bg = GameObject.Find("Background");
        Transform player = GameObject.Find("Player").transform;
        Vector3 pos = player.position + new Vector3(0, 0, -5);
        transform.position += (pos - transform.position) * 6 * Time.deltaTime;
        bg.transform.position = transform.position + new Vector3(0, 0, 50);
    }
}
