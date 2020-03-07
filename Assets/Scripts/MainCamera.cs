using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainCamera : MonoBehaviour {
    void Update() {
        Transform player = GameObject.Find("Player").GetComponent<Transform>();
        Vector3 pos = player.position + new Vector3(0, 0, -5);
        transform.position += (pos - transform.position) * 6 * Time.deltaTime;
    }
}
