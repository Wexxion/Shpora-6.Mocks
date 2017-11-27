using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using FakeItEasy;
using FileSender.Dependencies;
using FluentAssertions;
using NUnit.Framework;

namespace FileSender
{
    public class FileSender
    {
        private readonly ICryptographer cryptographer;
        private readonly ISender sender;
        private readonly IRecognizer recognizer;

        public FileSender(ICryptographer cryptographer,
            ISender sender,
            IRecognizer recognizer)
        {
            this.cryptographer = cryptographer;
            this.sender = sender;
            this.recognizer = recognizer;
        }

        public Result SendFiles(File[] files, X509Certificate certificate)
        {
            return new Result
            {
                SkippedFiles = files
                    .Where(file => !TrySendFile(file, certificate))
                    .ToArray()
            };
        }

        private bool TrySendFile(File file, X509Certificate certificate)
        {
            Document document;
            if (!recognizer.TryRecognize(file, out document))
                return false;
            if (!CheckFormat(document) || !CheckActual(document))
                return false;
            var signedContent = cryptographer.Sign(document.Content, certificate);
            return sender.TrySend(signedContent);
        }

        private bool CheckFormat(Document document)
        {
            return document.Format == "4.0" ||
                   document.Format == "3.1";
        }

        private bool CheckActual(Document document)
        {
            return document.Created.AddMonths(1) > DateTime.Now;
        }

        public class Result
        {
            public File[] SkippedFiles { get; set; }
        }
    }

    //TODO: реализовать недостающие тесты
    [TestFixture]
    public class FileSender_Should
    {
        private FileSender fileSender;
        private ICryptographer cryptographer;
        private ISender sender;
        private IRecognizer recognizer;

        private readonly X509Certificate certificate = new X509Certificate();
        private File file;
        private byte[] signedContent;
        private Document document;

        [SetUp]
        public void SetUp()
        {
            // Постарайтесь вынести в SetUp всё неспецифическое конфигурирование так,
            // чтобы в конкретных тестах осталась только специфика теста,
            // без конфигурирования "обычного" сценария работы

            file = new File("someFile", new byte[] {1, 2, 3});
            signedContent = new byte[] {1, 7};

            cryptographer = A.Fake<ICryptographer>();
            sender = A.Fake<ISender>();
            recognizer = A.Fake<IRecognizer>();

            document = new Document(file.Name, file.Content, DateTime.Now, "4.0");
            A.CallTo(() => recognizer.TryRecognize(file, out document))
                .WithAnyArguments().Returns(true).AssignsOutAndRefParametersLazily(x => new[] {document});
            A.CallTo(() => cryptographer.Sign(document.Content, certificate))
                .WithAnyArguments()
                .Returns(signedContent);
            A.CallTo(() => sender.TrySend(signedContent))
                .WithAnyArguments()
                .Returns(true);

            fileSender = new FileSender(cryptographer, sender, recognizer);
        }

        [TestCase("4.0")]
        [TestCase("3.1")]
        public void Send_WhenGoodFormat(string format)
        {
            document = new Document("Doc", new byte[] { 1, 2, 3 }, DateTime.Now, format);
            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().BeEmpty();
            A.CallTo(() => sender.TrySend(A<byte[]>.Ignored))
                .MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void Skip_WhenBadFormat()
        {
            document = new Document("Doc", new byte[] { 1, 2, 3 }, DateTime.Now, "Trash");

            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().Contain(file);

            A.CallTo(() => recognizer.TryRecognize(A<File>.Ignored, out document))
                .MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => sender.TrySend(A<byte[]>.Ignored))
                .MustHaveHappened(Repeated.Never);
        }

        [Test]
        public void Skip_WhenOlderThanAMonth()
        {
            document = new Document("Doc", new byte[] { 1, 2, 3 }, DateTime.MinValue, "Trash");

            fileSender.SendFiles(new[] { file }, certificate)
                .SkippedFiles.Should().Contain(file);

            A.CallTo(() => recognizer.TryRecognize(A<File>.Ignored, out document))
                .MustHaveHappened(Repeated.Exactly.Once);
            A.CallTo(() => sender.TrySend(A<byte[]>.Ignored))
                .MustHaveHappened(Repeated.Never);
        }

        [Test]
        public void Send_WhenYoungerThanAMonth()
        {
            document = new Document("Doc", new byte[] { 1, 2, 3 }, DateTime.Now.AddDays(-14), "4.0");

            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().NotContain(file);

            A.CallTo(() => sender.TrySend(A<byte[]>.Ignored))
                .MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void Skip_WhenSendFails()
        {
            A.CallTo(() => sender.TrySend(null)).WithAnyArguments()
                .Returns(false).Once();

            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().Contain(file);

            A.CallTo(() => sender.TrySend(A<byte[]>.Ignored))
                .MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void Skip_WhenNotRecognized()
        {
            A.CallTo(() => recognizer.TryRecognize(null, out document))
                .WithAnyArguments().Returns(false).Once();

            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().Contain(file);

            A.CallTo(() => sender.TrySend(A<byte[]>.Ignored))
                .MustHaveHappened(Repeated.Never);
        }

        [Test]
        public void IndependentlySend_WhenSeveralFiles1()
        {
            var file1 = new File("someFile1", new byte[] {1, 2, 3});

            fileSender.SendFiles(new[] {file, file1}, certificate)
                .SkippedFiles.Should().BeEmpty();

            A.CallTo(() => sender.TrySend(A<byte[]>.Ignored))
                .MustHaveHappened(Repeated.Exactly.Twice);
        }


        [Test]
        public void IndependentlySend_WhenSeveralFiles2()
        {
            var file1 = new File("someFile1", new byte[] { 1, 2, 3 });
            var file2 = new File("someFile2", new byte[] { 1, 2, 3 });

            A.CallTo(() => recognizer.TryRecognize(null, out document))
                .WithAnyArguments().Returns(false).Once();

            fileSender.SendFiles(new[] { file, file1, file2 }, certificate)
                .SkippedFiles.Should().Contain(file);

            A.CallTo(() => sender.TrySend(A<byte[]>.Ignored))
                .MustHaveHappened(Repeated.Exactly.Twice);
        }
    }
}