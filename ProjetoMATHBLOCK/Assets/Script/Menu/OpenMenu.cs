using UnityEngine;

public class OpenMenu : MonoBehaviour
{
    public GameObject menu;
    public Animator animatorMenu;

    public void AbrirMenu()
    {
        menu.SetActive(true);
        animatorMenu.Play("OpenPaper", 0, 0f);
    }
}