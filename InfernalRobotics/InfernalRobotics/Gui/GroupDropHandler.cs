using UnityEngine;
using UnityEngine.EventSystems;

using InfernalRobotics_v3.Command;


namespace InfernalRobotics_v3.Gui
{
	// IDropHandler is not used anymore, we make calls directly from the DragHandler

	public class GroupDropHandler : MonoBehaviour//, IDropHandler
	{
	/*
		public void OnDrop(PointerEventData eventData)
		{
			var droppedObject = eventData.pointerDrag;
			var dragHandler = droppedObject.GetComponent<GroupDragHandler>();

			onGroupDrop(dragHandler);
		}
	*/
		public void onGroupDrop(GroupDragHandler dragHandler)
		{
			var groupUIControls = dragHandler.draggedItem;
			int insertAt = dragHandler.placeholder.transform.GetSiblingIndex();

			foreach(var pair in WindowManager._servoGroupUIControls)
			{
				if(pair.Value == groupUIControls)
				{
					var g = pair.Key;

					Controller.Instance.ServoGroups.Remove(g);
					Controller.Instance.ServoGroups.Insert(insertAt, g);

					break;
				}
			}
		}
	}

}