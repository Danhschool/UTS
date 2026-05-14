using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.Commands
{
    [CreateAssetMenu(fileName = "Stop Action", menuName = "Units/Commands/Stop", order = 101)]
    public class StopCommand : BaseCommand
    {
        public override bool CanHandle(CommandContext context)
        {
            // Stop chỉ dùng từ UI / ActivateAction (MouseButton.Left mặc định), không cướp chuột phải ra lệnh di chuyển.
            return context.Commandable is AbstractUnit && context.Button != MouseButton.Right;
        }

        public override void Handle(CommandContext context)
        {
            AbstractUnit unit = (AbstractUnit)context.Commandable;
            unit.Stop();
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}