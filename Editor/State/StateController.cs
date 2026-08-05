// Copyright (C) GameWright. Licensed under MIT.

using System.Collections.Generic;

namespace GameWright.Editor.State
{
    internal enum GameWrightState
    {
        Initialized,
        ExecutingAllFunctions,
        ExecutingFunction
    }

    internal interface IStateController
    {
        GameWrightState CurrentState { get; }

        void SetState(GameWrightState state);
        void ReturnToPreviousState();
        void ClearState();
    }

    internal class StateController : IStateController
    {
        private readonly Stack<GameWrightState> _stateHistory = new Stack<GameWrightState>();
        private GameWrightState _currentState = GameWrightState.Initialized;

        public GameWrightState CurrentState => _currentState;

        public void SetState(GameWrightState state)
        {
            if (_currentState == state) return;

            _stateHistory.Push(_currentState);
            _currentState = state;
        }

        public void ReturnToPreviousState()
        {
            _currentState = _stateHistory.Count > 0
                ? _stateHistory.Pop()
                : GameWrightState.Initialized;
        }

        public void ClearState()
        {
            _stateHistory.Clear();
            _currentState = GameWrightState.Initialized;
        }
    }
}
