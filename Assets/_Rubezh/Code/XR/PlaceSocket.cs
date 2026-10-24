using Rubezh.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Rubezh.XR
{
    // Место для предмета (NG-09, layout.md §3): сокет с фильтром по Interaction Layer и метка, какое
    // это место для директора и у какого якоря оно стоит. Шахта магазина — тоже место (InWeapon).
    [RequireComponent(typeof(XRSocketInteractor))]
    public sealed class PlaceSocket : MonoBehaviour
    {
        [SerializeField] private Place place = Place.Unknown;
        [SerializeField] private Anchor anchor = Anchor.None;

        private XRSocketInteractor _socket;

        public Place Place => place;
        public Anchor Anchor => anchor;
        public XRSocketInteractor Socket => _socket != null ? _socket : _socket = GetComponent<XRSocketInteractor>();

        public void Configure(Place placeId, Anchor anchorId)
        {
            place = placeId;
            anchor = anchorId;
        }
    }
}
