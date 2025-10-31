using UnityEngine;

public class CarHealth : MonoBehaviour
{
    uint hp = 100;

    public bool Deal_Damage(uint damage)
    {
        // dont want overflow
        if (hp >= damage)
            hp -= damage;

        return hp == 0;
    }

    public bool Heal(uint heal)
    {
        // dont want overflow
        if (hp <= uint.MaxValue - heal)
            hp += heal;

        return hp != uint.MaxValue;
    }
}
