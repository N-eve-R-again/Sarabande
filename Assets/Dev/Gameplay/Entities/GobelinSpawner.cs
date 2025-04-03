using UnityEngine;

public class GobelinSpawner : MonoBehaviour
{
    public GameObject theUltimateWarrior;
    private float t;
    private int numberOfGobelins;
    public string[] cuteNames;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //the goblining never stops, so it never starts. 
    }

    // Update is called once per frame
    void Update()
    {
        if (numberOfGobelins < 30) // I lied, it stops
        {
            if (t > 0)
            {
                t -= Time.deltaTime; //time is running out
            }
            else
            {
                t = 1f;
                float x = Random.Range(-16f, 16f); float y = Random.Range(-10f, 10f);

                GameObject gobi = Instantiate(theUltimateWarrior,new Vector3(x,y,5f),Quaternion.identity); //goblin the goblinum
                gobi.name = cuteNames[Random.Range(0,cuteNames.Length)];
                numberOfGobelins++; //keep track of the goblinness of the situation
            }
        }
    }
}
