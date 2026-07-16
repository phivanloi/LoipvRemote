using LoipvRemote.Messages;
using LoipvRemote.Messages.MessageFilteringOptions;
using LoipvRemote.Messages.MessageWriters;
using LoipvRemote.Messages.WriterDecorators;
using NSubstitute;
using NUnit.Framework;

namespace LoipvRemoteTests.Messages.MessageWriters
{
    public class MessageTypeFilterDecoratorTests
    {
        private MessageTypeFilterDecorator _sut;
        private IMessageWriter _mockWriter;
        private IMessageTypeFilteringOptions _filter;
        private IMessage _message;

        [SetUp]
        public void Setup()
        {
            _mockWriter = Substitute.For<IMessageWriter>();
            _filter = Substitute.For<IMessageTypeFilteringOptions>();
            _sut = new MessageTypeFilterDecorator(_filter, _mockWriter);
            _message = Substitute.For<IMessage>();
        }

        [Test]
        public void DebugMessageWrittenIfAllowed()
        {
        _message.MessageClass.Returns(MessageClass.DebugMsg);
            _filter.AllowDebugMessages.Returns(true);
            _sut.Write(_message);
            _mockWriter.Received().Write(_message);
        }

        [Test]
        public void DebugMessageNotWrittenIfNotAllowed()
        {
        _message.MessageClass.Returns(MessageClass.DebugMsg);
            _filter.AllowDebugMessages.Returns(false);
            _sut.Write(_message);
            _mockWriter.DidNotReceive().Write(_message);
        }

        [Test]
        public void InfoMessageWrittenIfAllowed()
        {
        _message.MessageClass.Returns(MessageClass.InformationMsg);
            _filter.AllowInfoMessages.Returns(true);
            _sut.Write(_message);
            _mockWriter.Received().Write(_message);
        }

        [Test]
        public void InfoMessageNotWrittenIfNotAllowed()
        {
        _message.MessageClass.Returns(MessageClass.InformationMsg);
            _filter.AllowInfoMessages.Returns(false);
            _sut.Write(_message);
            _mockWriter.DidNotReceive().Write(_message);
        }

        [Test]
        public void WarningMessageWrittenIfAllowed()
        {
        _message.MessageClass.Returns(MessageClass.WarningMsg);
            _filter.AllowWarningMessages.Returns(true);
            _sut.Write(_message);
            _mockWriter.Received().Write(_message);
        }

        [Test]
        public void WarningMessageNotWrittenIfNotAllowed()
        {
        _message.MessageClass.Returns(MessageClass.WarningMsg);
            _filter.AllowWarningMessages.Returns(false);
            _sut.Write(_message);
            _mockWriter.DidNotReceive().Write(_message);
        }

        [Test]
        public void ErrorMessageWrittenIfAllowed()
        {
        _message.MessageClass.Returns(MessageClass.ErrorMsg);
            _filter.AllowErrorMessages.Returns(true);
            _sut.Write(_message);
            _mockWriter.Received().Write(_message);
        }

        [Test]
        public void ErrorMessageNotWrittenIfNotAllowed()
        {
        _message.MessageClass.Returns(MessageClass.ErrorMsg);
            _filter.AllowErrorMessages.Returns(false);
            _sut.Write(_message);
            _mockWriter.DidNotReceive().Write(_message);
        }
    }
}
