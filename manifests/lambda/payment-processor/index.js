const { SQSClient, ReceiveMessageCommand, DeleteMessageCommand } = require('@aws-sdk/client-sqs');
const { RDSDataClient, ExecuteStatementCommand } = require('@aws-sdk/client-rds-data');
const { SNSClient, PublishCommand } = require('@aws-sdk/client-sns');

const sqsClient = new SQSClient({ region: process.env.AWS_REGION || 'us-east-2' });
const snsClient = new SNSClient({ region: process.env.AWS_REGION || 'us-east-2' });

// Para RDS MySQL via HTTP API (Data API)
const rdsClient = new RDSDataClient({ region: process.env.AWS_REGION || 'us-east-2' });

const QUEUE_URL = process.env.SQS_QUEUE_URL;
const SNS_TOPIC_ARN = process.env.SNS_TOPIC_ARN;
const RDS_SECRET_ARN = process.env.RDS_SECRET_ARN;
const RDS_RESOURCE_ARN = process.env.RDS_RESOURCE_ARN;
const DATABASE_NAME = process.env.DATABASE_NAME || 'fcg_payments';

/**
 * Handler principal da Lambda
 * Processa mensagens da fila SQS fcg-payments-requested
 */
exports.handler = async (event) => {
  console.log('Lambda triggered with event:', JSON.stringify(event));

  try {
    // Recebe mensagens da fila SQS
    const receiveParams = {
      QueueUrl: QUEUE_URL,
      MaxNumberOfMessages: 10,
      WaitTimeSeconds: 5,
      VisibilityTimeout: 30
    };

    const receiveCommand = new ReceiveMessageCommand(receiveParams);
    const receiveResult = await sqsClient.send(receiveCommand);

    if (!receiveResult.Messages || receiveResult.Messages.length === 0) {
      console.log('No messages to process');
      return {
        statusCode: 200,
        body: JSON.stringify({ message: 'No messages' })
      };
    }

    console.log(`Processing ${receiveResult.Messages.length} messages`);

    // Processa cada mensagem
    for (const message of receiveResult.Messages) {
      try {
        const paymentData = JSON.parse(message.Body);
        console.log('Processing payment:', paymentData);

        // Atualiza o pagamento no MySQL
        await updatePaymentStatus(paymentData.paymentId, 'Paid');

        // Publica notificação no SNS
        await publishNotification(paymentData);

        // Remove mensagem da fila
        await deleteMessage(message.ReceiptHandle);

        console.log(`Payment ${paymentData.paymentId} processed successfully`);
      } catch (error) {
        console.error('Error processing message:', error);
        // Mensagem volta para a fila após visibility timeout
      }
    }

    return {
      statusCode: 200,
      body: JSON.stringify({ 
        processed: receiveResult.Messages.length 
      })
    };

  } catch (error) {
    console.error('Lambda error:', error);
    return {
      statusCode: 500,
      body: JSON.stringify({ 
        error: error.message 
      })
    };
  }
};

/**
 * Atualiza o status do pagamento no MySQL usando RDS Data API
 */
async function updatePaymentStatus(paymentId, status) {
  const sql = `
    UPDATE payments 
    SET status = :status, updated_at_utc = UTC_TIMESTAMP()
    WHERE id = :paymentId
  `;

  const params = {
    secretArn: RDS_SECRET_ARN,
    resourceArn: RDS_RESOURCE_ARN,
    database: DATABASE_NAME,
    sql: sql,
    parameters: [
      { name: 'status', value: { stringValue: status } },
      { name: 'paymentId', value: { stringValue: paymentId } }
    ]
  };

  const command = new ExecuteStatementCommand(params);
  const result = await rdsClient.send(command);
  
  console.log('Payment updated in MySQL:', result);
  return result;
}

/**
 * Publica notificação no SNS
 */
async function publishNotification(paymentData) {
  const message = {
    paymentId: paymentData.paymentId,
    userId: paymentData.userId,
    gameId: paymentData.gameId,
    amount: paymentData.amount,
    status: 'Paid',
    confirmedAtUtc: new Date().toISOString()
  };

  const params = {
    TopicArn: SNS_TOPIC_ARN,
    Message: JSON.stringify(message),
    Subject: 'FCG - Payment Confirmed'
  };

  const command = new PublishCommand(params);
  const result = await snsClient.send(command);
  
  console.log('Notification published to SNS:', result);
  return result;
}

/**
 * Remove mensagem da fila SQS
 */
async function deleteMessage(receiptHandle) {
  const params = {
    QueueUrl: QUEUE_URL,
    ReceiptHandle: receiptHandle
  };

  const command = new DeleteMessageCommand(params);
  await sqsClient.send(command);
  console.log('Message deleted from SQS');
}
