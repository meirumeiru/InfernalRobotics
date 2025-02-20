using UnityEngine;
using UnityEngine.EventSystems;

using InfernalRobotics_v3.Command;


namespace InfernalRobotics_v3.Gui
{
	// IDropHandler is not used anymore, we make calls directly from the DragHandler

	public class ServoDropHandler : MonoBehaviour//, IDropHandler
	{
	/*
		public void OnDrop(PointerEventData eventData)
		{
			var dropedObject = eventData.pointerDrag;
			var dragHandler = dropedObject.GetComponent<ServoDragHandler>();

			onServoDrop(dragHandler);
		}
	*/
		public void onServoDrop(ServoDragHandler dragHandler)
		{
			var servoUIControls = dragHandler.draggedItem;
			int insertAt = dragHandler.placeholder.transform.GetSiblingIndex();

			foreach(var pair in WindowManager._servoUIControls)
			{
				if(pair.ui == servoUIControls)
				{
					var newGroupIndex = dragHandler.dropZone.parent.GetSiblingIndex();
					Controller.MoveServo(pair.group.group, Controller.Instance.ServoGroups[newGroupIndex], insertAt, pair.servo.servo);

					if(Gui.WindowManager.Instance != null)
						Gui.WindowManager.Instance.Invalidate();

					break;
				}
			}
		}
	}
}