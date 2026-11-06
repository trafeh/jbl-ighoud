using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JbelIghoud.UI
{
    /// <summary>
    /// Detects mouse hover or navigation selection on menu buttons
    /// to trigger dynamic interactive audio/visual responses (e.g. spinning gears).
    /// </summary>
    public class MenuHoverTrigger : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public Action onHoverEnter;

        public void OnPointerEnter(PointerEventData eventData)
        {
            onHoverEnter?.Invoke();
        }

        public void OnSelect(BaseEventData eventData)
        {
            onHoverEnter?.Invoke();
        }
    }
}
