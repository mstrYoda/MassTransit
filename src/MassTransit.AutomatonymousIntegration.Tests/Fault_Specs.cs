﻿// Copyright 2007-2014 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.AutomatonymousTests
{
    using System;
    using System.Threading.Tasks;
    using Automatonymous;
    using NUnit.Framework;
    using Saga;
    using TestFramework;


    [TestFixture]
    public class When_an_activity_throws_an_exception :
        InMemoryTestFixture
    {
//
//        [Test]
//        public void Should_be_able_to_observe_its_own_event_fault()
//        {
//            var message = new Initialize();
//            Bus.Publish(message);
//
//            var initalizedSaga = _repository.ShouldContainSagaInState(message.CorrelationId, _machine.WaitingToStart, _machine, 8.Seconds());
//            Assert.IsNotNull(initalizedSaga);
//
//            Bus.Publish(new Start(message.CorrelationId));
//
//            var faultedSaga = _repository.ShouldContainSagaInState(message.CorrelationId, _machine.FailedToStart, _machine, 8.Seconds());
//            Assert.IsNotNull(faultedSaga);
//        }

        protected override void ConfigureInputQueueEndpoint(IReceiveEndpointConfigurator configurator)
        {
            _machine = new TestStateMachine();
            _repository = new InMemorySagaRepository<Instance>();

            configurator.StateMachineSaga(_machine, _repository, x =>
            {
                x.Correlate(_machine.StartFaulted, (saga, message) => saga.CorrelationId == message.Message.CorrelationId);
            });
        }

        TestStateMachine _machine;
        InMemorySagaRepository<Instance> _repository;


        class Instance :
            SagaStateMachineInstance
        {
            public Instance(Guid correlationId)
            {
                CorrelationId = correlationId;
            }

            protected Instance()
            {
            }

            public State CurrentState { get; set; }
            public Guid CorrelationId { get; set; }
        }


        class TestStateMachine :
            AutomatonymousStateMachine<Instance>
        {
            public TestStateMachine()
            {
                InstanceState(x => x.CurrentState);

                State(() => Running);
                State(() => WaitingToStart);
                State(() => FailedToStart);

                Event(() => Started);
                Event(() => Initialized);
                Event(() => Created);
                Event(() => StartFaulted);

                Initially(
                    When(Started)
                        .Then(context =>
                        {
                            throw new NotSupportedException("This is expected, but nonetheless exceptional");
                        })
                        .TransitionTo(Running),
                    When(Initialized)
                        .TransitionTo(WaitingToStart),
                    When(Created)
                        .Then(context =>
                        {
                            throw new NotSupportedException("This is expected, but nonetheless exceptional");
                        })
                        .TransitionTo(Running));

                During(WaitingToStart,
                    When(Started)
                        .Then(instance =>
                        {
                            throw new NotSupportedException("This is expected, but nonetheless exceptional");
                        })
                        .TransitionTo(Running),
                    When(StartFaulted)
                        .TransitionTo(FailedToStart));
            }

            public State WaitingToStart { get; private set; }
            public State FailedToStart { get; private set; }
            public State Running { get; private set; }

            public Event<Start> Started { get; private set; }
            public Event<Initialize> Initialized { get; private set; }
            public Event<Create> Created { get; private set; }
            public Event<Fault<Start>> StartFaulted { get; private set; }
        }


        public class Start :
            CorrelatedBy<Guid>
        {
            public Start()
            {
                CorrelationId = NewId.NextGuid();
            }

            public Start(Guid correlationId)
            {
                CorrelationId = correlationId;
            }

            public Guid CorrelationId { get; set; }
        }


        public class Initialize :
            CorrelatedBy<Guid>
        {
            public Initialize()
            {
                CorrelationId = NewId.NextGuid();
            }

            public Guid CorrelationId { get; set; }
        }


        public class Create :
            CorrelatedBy<Guid>
        {
            public Create()
            {
                CorrelationId = NewId.NextGuid();
            }

            public Guid CorrelationId { get; set; }
        }


        public class StartupComplete
        {
            public Guid TransactionId { get; set; }
        }


        [Test]
        public async void Should_be_received_as_a_fault_message()
        {
            var message = new Start();

            Task<ConsumeContext<Fault<Start>>> faultReceived =
                SubscribeHandler<Fault<Start>>(x => (message.CorrelationId == x.Message.Message.CorrelationId));

            await InputQueueSendEndpoint.Send(message);

            ConsumeContext<Fault<Start>> fault = await faultReceived;

            Assert.AreEqual(message.CorrelationId, fault.Message.Message.CorrelationId);
        }

        [Test]
        public async void Should_observe_the_fault_message()
        {
            var message = new Initialize();

            Task<ConsumeContext<Fault<Start>>> faultReceived =
                SubscribeHandler<Fault<Start>>(x => (message.CorrelationId == x.Message.Message.CorrelationId));

            await InputQueueSendEndpoint.Send(message);

            Guid? saga = await _repository.ShouldContainSaga(
    x => x.CorrelationId == message.CorrelationId && x.CurrentState == _machine.WaitingToStart, TestTimeout);


            await InputQueueSendEndpoint.Send(new Start(message.CorrelationId));

            ConsumeContext<Fault<Start>> fault = await faultReceived;

            Assert.AreEqual(message.CorrelationId, fault.Message.Message.CorrelationId);

             saga = await _repository.ShouldContainSaga(
                x => x.CorrelationId == message.CorrelationId && x.CurrentState == _machine.FailedToStart, TestTimeout);

            Assert.IsTrue(saga.HasValue);
        }
    }
}