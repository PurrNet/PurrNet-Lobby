using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PurrLobby
{
    public class ViewManager : MonoBehaviour
    {
        [SerializeField] private List<View> allViews = new();
        [SerializeField] private View defaultView;

        private void Start()
        {
            foreach (var view in allViews)
            {
                HideViewInternal(view);
            }
            ShowViewInternal(defaultView);
        }

        public void ShowView<T>(bool hideOthers = true) where T : View
        {
            foreach (var view in allViews)
            {
                if (!view)
                    continue;
                if (view.GetType() == typeof(T))
                {
                    ShowViewInternal(view);
                }
                else
                {
                    if(hideOthers)
                        HideViewInternal(view);
                }
            }
        }

        public void HideView<T>() where T : View
        {
            foreach (var view in allViews)
            {
                if(view.GetType() == typeof(T))
                    HideViewInternal(view);
            }
        }

        private void ShowViewInternal(View view)
        {
            view.canvasGroup.alpha = 1;
            view.canvasGroup.interactable = true;
            view.canvasGroup.blocksRaycasts = true;
            view.OnShow();
            view.OnViewShow?.Invoke();
        }

        private void HideViewInternal(View view)
        {
            if(!view)
                return;

            if (view.canvasGroup)
            {
                view.canvasGroup.alpha = 0;
                view.canvasGroup.interactable = false;
                view.canvasGroup.blocksRaycasts = false;
            }
            
            view.OnHide();
            view.OnViewHide?.Invoke();
        }

        #region Events

        public void OnRoomJoined()
        {
            ShowView<LobbyView>();
        }   
        
        public void OnRoomLeft()
        {
            ShowView<MainMenuView>();
        }

        public void OnBrowseClicked()
        {
            ShowView<BrowseView>();
        }
        
        public void OnRoomCreateClicked()
        {
            ShowView<CreatingRoomView>(false);
        }
        
        public void OnJoiningRoom()
        {
            ShowView<LoadingRoomView>(false);
        }

        public void OnLeaveBrowseClicked()
        {
            ShowView<MainMenuView>();
        }

        #endregion
    }
    
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class View : MonoBehaviour
    {
        private CanvasGroup _canvasGroup;
        public CanvasGroup canvasGroup => _canvasGroup;

        public UnityEvent OnViewShow = new();
        public UnityEvent OnViewHide = new();

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        public virtual void OnShow() {}
        public virtual void OnHide() {}
    }
}
