namespace HearthstoneServer.Managers;

/// <summary>
/// 观察者接口。
/// GameManager 在关键事件发生时通知所有已注册的 Observer。
/// </summary>
public interface IObserver
{
    void ActionStart(BlockType type, Entities.BaseEntity source, int index, Entities.BaseEntity? target) { }
    void ActionEnd(BlockType type, Entities.BaseEntity source) { }
    void GameStep(Step step, Step? nextStep) { }
    void NewEntity(Entities.BaseEntity entity) { }
    void StartGame() { }
    void Turn(Entities.BaseEntity player) { }
    void GameAction(object action, Entities.BaseEntity source, params object[] args) { }
    void TargetedAction(object action, Entities.BaseEntity source, Entities.BaseEntity target, params object[] args) { }
}
