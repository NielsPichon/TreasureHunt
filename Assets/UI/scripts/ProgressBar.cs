using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ProgressBar : MonoBehaviour
{
    private Slider slider;

    // Start is called before the first frame update
    void Awake()
    {
        slider = gameObject.GetComponent<Slider>();
        slider.value = 0;
    }

    // Update is called once per frame
    void Update()
    {
        slider.value += Time.deltaTime;
    }

    void LateUpdate()
    {
        transform.LookAt(Camera.main.transform.position, -Vector3.up);
    }

    public void SetMaxTime(float time)
    {
        slider.maxValue = time;
    }

}
